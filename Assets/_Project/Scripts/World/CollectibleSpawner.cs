using System.Collections.Generic;
using UnityEngine;

public class CollectibleSpawner : MonoBehaviour
{
    [SerializeField] NoiseTerrainGenerator generator;
    [SerializeField] CatalogEntry[] possibleEntries;
    [SerializeField] Material collectibleMaterial;
    [SerializeField] StructureScatter structures;
    [SerializeField] PropScatter props;
    [SerializeField] CoverWell wells;
    [Tooltip("Color base del planeta, para que las paredes queden dentro de su paleta.")]
    [SerializeField] Color planetColor = new Color(0.435f, 0.788f, 0.831f);

    [Header("Luz de las fichas")]
    [Tooltip("Alcance del resplandor. Corto a propósito: sirve para distinguir la ficha del suelo cuando ya la tienes cerca, no para verla de lejos.")]
    [SerializeField] float itemLightRange = 2.4f;
    [SerializeField] float itemLightIntensity = 1.3f;

    [Header("Cantidad")]
    [SerializeField] int minCollectibles = 4;
    [SerializeField] int maxCollectibles = 10;

    [Header("Distribución: lejos del jugador, hacia los márgenes de lo alcanzable")]
    [SerializeField] float minDistanceFromPlayer = 20f;
    [Tooltip("Fracción de celdas elegibles (las más lejanas) cuando se genera la MENOR cantidad posible de fichas. Chica = solo las más lejanas.")]
    [SerializeField, Range(0.02f, 1f)] float poolFractionAtFewCollectibles = 0.15f;
    [Tooltip("Fracción de celdas elegibles cuando se genera la MAYOR cantidad posible de fichas. No hay espacio lejano para todas, así que se relaja.")]
    [SerializeField, Range(0.02f, 1f)] float poolFractionAtManyCollectibles = 0.6f;

    [Tooltip("Distancia mínima entre dos fichas. Si quedan pegadas se juntan de un saque y el radar pierde sentido.")]
    [SerializeField] float minItemSeparation = 22f;

    readonly Dictionary<CatalogEntry, Material> accentMaterials = new Dictionary<CatalogEntry, Material>();

    void OnEnable()
    {
        if (generator != null) generator.OnTerrainReady += HandleTerrainReady;
    }

    void OnDisable()
    {
        if (generator != null) generator.OnTerrainReady -= HandleTerrainReady;
    }

    void HandleTerrainReady(Vector3 spawnPoint)
    {
        // Las fichas salen del tramo del cuerpo asignado, no de una lista fija:
        // así el lore avanza junto con la ruta.
        var session = PlanetSession.Instance;
        int tramo = session != null ? session.Tramo : 1;
        possibleEntries = CatalogDatabase.EntriesForTramo(tramo);

        if (possibleEntries.Length == 0)
        {
            Debug.LogWarning("La pool de objetos quedó vacía: revisar Resources/items.json.");
            return;
        }

        int minimum = minCollectibles;
        int maximum = maxCollectibles;
        if (session != null && session.Body != null)
        {
            minimum = session.Body.itemsMin;
            maximum = session.Body.itemsMax;
        }

        int count = Random.Range(minimum, maximum + 1);

        // Menos fichas => pool más chico y estricto (solo las celdas más lejanas).
        // Más fichas => pool más amplio, porque no alcanzan celdas lejanas para todas.
        float countT = maximum > minimum
            ? Mathf.InverseLerp(minimum, maximum, count)
            : 0f;
        float poolFraction = Mathf.Lerp(poolFractionAtFewCollectibles, poolFractionAtManyCollectibles, countT);

        var candidates = new List<Vector3>();
        foreach (var pos in generator.WalkableCellPositions)
            if (Vector3.Distance(pos, spawnPoint) >= minDistanceFromPlayer)
                candidates.Add(pos);

        candidates.Sort((a, b) =>
            Vector3.Distance(b, spawnPoint).CompareTo(Vector3.Distance(a, spawnPoint)));

        int poolSize = Mathf.Max(1, Mathf.RoundToInt(candidates.Count * poolFraction));
        var outerPool = candidates.GetRange(0, Mathf.Min(poolSize, candidates.Count));
        Shuffle(outerPool);

        count = Mathf.Min(count, outerPool.Count);

        // Se eligen las posiciones respetando una separación mínima. Dos fichas pegadas
        // arruinan la exploración: las juntas de una y el radar deja de servir.
        var itemPositions = new List<Vector3>(count);
        foreach (var candidate in outerPool)
        {
            if (itemPositions.Count >= count) break;
            if (TooClose(candidate, itemPositions)) continue;
            itemPositions.Add(candidate);
        }

        // El total se cuenta sobre lo que realmente se colocó, no sobre lo que se
        // quería colocar: si no, el contador pediría fichas que no existen.
        if (Inventory.Instance == null)
            Debug.LogWarning("CollectibleSpawner no encontró un Inventory: el contador va a quedar en 0.");
        else
            Inventory.Instance.SetTotal(itemPositions.Count);

        Debug.Log($"Coleccionables: {itemPositions.Count} de {count} pedidos " +
                  $"(pool de {outerPool.Count}, separación mínima {minItemSeparation}).");

        for (int i = 0; i < itemPositions.Count; i++)
        {
            var entry = possibleEntries[i % possibleEntries.Length];
            SpawnCollectible(itemPositions[i], entry);
        }

        if (itemPositions.Count == 0)
            Debug.LogWarning("No hubo celdas alcanzables lejos del jugador para poner coleccionables.");
        else if (itemPositions.Count < count)
            Debug.LogWarning($"Solo entraron {itemPositions.Count} de {count} fichas respetando la separación mínima.");

        // Las estructuras se levantan después y encima de estas posiciones, así que
        // cada objeto queda dentro de una. Se llama directo en vez de por evento:
        // el orden importa y no queremos que dependa de quién se inicializa antes.
        if (structures != null)
            structures.BuildAround(itemPositions, generator.WalkableCellPositions, spawnPoint, planetColor);

        // Los cráteres van ANTES que los props: ocupan sitio y los props tienen que poder
        // esquivarlos, no plantarse dentro del labio.
        if (wells != null)
            wells.BuildAround(itemPositions, generator.WalkableCellPositions, spawnPoint);

        // Los props van al final y por llamada directa, por el mismo motivo: tienen
        // que saber dónde quedaron las fichas para no plantarse encima. Si esto
        // dependiera del orden de OnTerrainReady volveríamos a tener objetos
        // encerrados dentro de un árbol.
        if (props != null)
            props.ScatterAround(itemPositions, generator.WalkableCellPositions, spawnPoint);
    }

    void SpawnCollectible(Vector3 groundTop, CatalogEntry entry)
    {
        ItemVisualFactory.Spawn(entry, groundTop + Vector3.up * 0.25f, transform,
                                collectibleMaterial, GetAccentMaterial(entry),
                                itemLightRange, itemLightIntensity);
    }

    // Un material por ficha: son cuatro, así que no vale la pena algo más elaborado.
    Material GetAccentMaterial(CatalogEntry entry)
    {
        if (accentMaterials.TryGetValue(entry, out var existing)) return existing;

        var mat = new Material(collectibleMaterial != null
            ? collectibleMaterial.shader
            : Shader.Find("Cielo Profundo/PS1 Vertex Snap"));
        mat.SetColor("_BaseColor", entry.accentColor);
        mat.SetFloat("_SnapResolution", 140f);
        accentMaterials[entry] = mat;
        return mat;
    }

    bool TooClose(Vector3 candidate, List<Vector3> placed)
    {
        foreach (var p in placed)
            if (Vector3.Distance(candidate, p) < minItemSeparation) return true;
        return false;
    }

    void Shuffle(List<Vector3> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
