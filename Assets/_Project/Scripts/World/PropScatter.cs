using System;
using System.Collections.Generic;
using UnityEngine;

// Puebla el terreno con los props importados. Ya no genera cristales por código:
// instancia prefabs de los packs y les aplica el material del juego.
//
// No escucha OnTerrainReady. Lo llama CollectibleSpawner después de colocar las
// fichas, y le pasa dónde quedaron, porque el orden importa: es exactamente el
// problema que tuvimos con las estructuras, que se levantaban encima de los objetos
// y los dejaban encerrados. Acá los props esquivan las fichas, no al revés.
public class PropScatter : MonoBehaviour
{
    // Un grupo por familia de props. Cada familia se comporta distinto: un árbol
    // bloquea el paso y una planta no, y una piedra chica se repite mucho más que
    // una antena abandonada.
    [Serializable]
    public class PropGroup
    {
        public string nombre = "grupo";
        public GameObject[] prefabs;

        [Tooltip("Peso relativo al elegir qué plantar. No hace falta que sumen 1.")]
        public float peso = 1f;

        [Tooltip("Cuántas celdas pisables, en promedio, por cada prop de este grupo.")]
        public float celdasPorProp = 40f;

        public Vector2 escala = new Vector2(0.9f, 1.3f);

        [Tooltip("Inclinación aleatoria en grados. Poca: si se pasan, flotan o se entierran de canto.")]
        public float inclinacion = 5f;

        [Tooltip("Con collider bloquea el paso y sirve de cobertura. Sin collider es decorado que se atraviesa.")]
        public bool bloquea;

        [Tooltip("Distancia mínima a otro prop del mismo grupo.")]
        public float separacion = 4f;
    }

    [SerializeField] Material propMaterial;
    [SerializeField] PropGroup[] groups;

    [Header("Espacio libre")]
    [Tooltip("Radio despejado alrededor del punto donde aterrizas. Nada de abrir los ojos contra un árbol.")]
    [SerializeField] float clearanceFromSpawn = 12f;

    [Tooltip("Radio despejado alrededor de cada ficha. Tiene que quedar espacio para verla, alcanzarla y recogerla.")]
    [SerializeField] float clearanceFromItems = 6f;

    [SerializeField] int maxProps = 220;

    public void ScatterAround(IReadOnlyList<Vector3> itemPositions,
                              IReadOnlyList<Vector3> walkableCells,
                              Vector3 spawnPoint)
    {
        if (groups == null || groups.Length == 0) return;

        var free = new List<Vector3>();
        foreach (var cell in walkableCells)
        {
            if (Vector3.Distance(cell, spawnPoint) < clearanceFromSpawn) continue;
            if (TooCloseToAny(cell, itemPositions, clearanceFromItems)) continue;
            free.Add(cell);
        }

        if (free.Count == 0)
        {
            Debug.LogWarning("PropScatter: no quedaron celdas libres para plantar props.");
            return;
        }

        Shuffle(free);

        // Cada grupo pide su cantidad según la densidad, no un número fijo: un mapa
        // con poco suelo pisable recibe pocos props y no queda saturado.
        var placed = new Dictionary<PropGroup, List<Vector3>>();
        var queue = new List<PropGroup>();
        foreach (var g in groups)
        {
            if (g.prefabs == null || g.prefabs.Length == 0) continue;
            placed[g] = new List<Vector3>();

            int wanted = Mathf.FloorToInt(free.Count / Mathf.Max(1f, g.celdasPorProp) * Mathf.Max(0f, g.peso));
            for (int i = 0; i < wanted; i++) queue.Add(g);
        }

        if (queue.Count == 0) return;
        Shuffle(queue);

        int total = 0;
        int cursor = 0;

        foreach (var group in queue)
        {
            if (total >= maxProps) break;

            // Se busca una celda que respete la separación del grupo. Se prueban unas
            // pocas y se abandona: insistir sobre un mapa lleno cuesta y no aporta.
            Vector3 spot = Vector3.zero;
            bool found = false;
            for (int attempt = 0; attempt < 8 && cursor < free.Count; attempt++, cursor++)
            {
                if (TooCloseToAny(free[cursor], placed[group], group.separacion)) continue;
                spot = free[cursor];
                found = true;
                cursor++;
                break;
            }

            if (!found) continue;

            SpawnProp(group, spot);
            placed[group].Add(spot);
            total++;
        }

        Debug.Log($"Props: {total} plantados sobre {free.Count} celdas libres " +
                  $"(de {walkableCells.Count} pisables).");
    }

    void SpawnProp(PropGroup group, Vector3 groundTop)
    {
        var prefab = group.prefabs[UnityEngine.Random.Range(0, group.prefabs.Length)];
        if (prefab == null) return;

        var go = Instantiate(prefab, transform);
        go.transform.rotation = Quaternion.Euler(
            UnityEngine.Random.Range(-group.inclinacion, group.inclinacion),
            UnityEngine.Random.Range(0f, 360f),
            UnityEngine.Random.Range(-group.inclinacion, group.inclinacion));
        go.transform.localScale = Vector3.one *
            UnityEngine.Random.Range(group.escala.x, group.escala.y);

        // La escala y la rotación se aplican ANTES de medir: los pivotes de los packs
        // no están todos en la base, y midiendo el alto real se apoya bien cualquiera.
        go.transform.position = groundTop;

        var renderers = go.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            // Se hunde un poco a propósito: apoyado justo en la cara superior de la
            // celda, cualquier irregularidad del terreno deja ver el hueco de abajo.
            //
            // El hundimiento es proporcional al alto y no fijo. Las piedras del pack
            // miden un centímetro — son parches planos de suelo — y con un valor fijo
            // quedaban enterradas enteras. El epsilon de arriba evita que las caras
            // planas peleen con el terreno por el mismo píxel.
            float height = Mathf.Max(0.001f, bounds.size.y);
            float sink = Mathf.Min(0.12f, height * 0.12f);
            go.transform.position += Vector3.up * (groundTop.y - bounds.min.y - sink + 0.01f);

            if (propMaterial != null)
                foreach (var r in renderers) r.sharedMaterial = propMaterial;
        }

        if (!group.bloquea) return;

        // MeshCollider sin convexidad: son props estáticos, así que Unity los acepta
        // con la malla tal cual y la silueta que ves es contra la que chocas.
        foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            var col = filter.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = filter.sharedMesh;
        }
    }

    static bool TooCloseToAny(Vector3 candidate, IReadOnlyList<Vector3> others, float minDistance)
    {
        if (others == null) return false;
        for (int i = 0; i < others.Count; i++)
            if (Vector3.Distance(candidate, others[i]) < minDistance) return true;
        return false;
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
