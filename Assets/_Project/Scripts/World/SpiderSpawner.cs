using System.Collections.Generic;
using UnityEngine;

// La población crece a medida que juntas fichas: cuanto más completo el catálogo,
// más de ellas hay. Nunca aparecen a la vista — ver una materializarse de la nada
// mataría la ambientación de golpe.
public class SpiderSpawner : MonoBehaviour
{
    [SerializeField] NoiseTerrainGenerator generator;
    [SerializeField] Material spiderMaterial;

    [Tooltip("Ráfagas de parloteo. Es lo que deja detectar una araña sin verla, así que se reparten a cada una al crearla.")]
    [SerializeField] AudioClip[] chatterClips;
    [SerializeField] Transform player;
    [SerializeField] Camera viewCamera;

    [Header("Población")]
    [SerializeField] int baseCount = 2;
    [Tooltip("Cada cuántas fichas recogidas aparece una araña más.")]
    [SerializeField] int itemsPerExtraSpider = 3;
    [SerializeField] int maxSpiders = 9;

    [Header("Progresión de la ruta")]
    [Tooltip("Arañas de más por cada tramo por encima del primero. La ruta tiene tres.")]
    [SerializeField] int spidersPerTramo = 1;

    [Tooltip("Cuánto mejora el oído por cada tramo por encima del primero. 0.15 = un 15% más de radio en el segundo tramo y un 30% en el tercero.")]
    [SerializeField] float hearingPerTramo = 0.15f;

    [Tooltip("Cuánto crece el oído entre el catálogo vacío y el catálogo lleno. Es la palanca principal de la curva: en un mapa de 243 unidades, oír más lejos se nota muchísimo más que tener más bichos.")]
    [SerializeField] float hearingAtFullCatalogue = 0.45f;

    [Tooltip("Cuánto se acelera la persecución con el catálogo lleno. Suave: si corren demasiado dejan de poder despistarse y el sigilo pierde sentido.")]
    [SerializeField] float huntSpeedAtFullCatalogue = 0.2f;

    [Header("Dónde pueden aparecer")]
    [SerializeField] float minDistanceFromSpawn = 45f;
    [Tooltip("Distancia mínima al jugador en el momento de aparecer.")]
    [SerializeField] float minDistanceFromPlayer = 40f;
    [Tooltip("Margen extra alrededor del cuadro. Con 0 podrían aparecer justo en el borde de la pantalla.")]
    [SerializeField, Range(0f, 0.5f)] float viewportMargin = 0.15f;

    // Tramo del cuerpo que se está visitando, 1 a 3. Si no hay sesión —abrir la escena
    // del planeta suelta desde el editor— vale 1 y todo sigue funcionando.
    static int Tramo => PlanetSession.Instance != null ? PlanetSession.Instance.Tramo : 1;

    readonly List<SpiderCreature> spiders = new List<SpiderCreature>();
    bool terrainReady;
    bool avisoDeCierre;
    bool subscribed;

    void OnEnable()
    {
        if (generator != null) generator.OnTerrainReady += HandleTerrainReady;
    }

    void OnDisable()
    {
        if (generator != null) generator.OnTerrainReady -= HandleTerrainReady;
    }

    // En Start y no en OnEnable: Inventory vive en otro GameObject y el orden de los
    // Awake entre objetos no está garantizado.
    void Start()
    {
        if (Inventory.Instance == null) return;
        Inventory.Instance.OnChanged += UpdatePopulation;
        subscribed = true;
    }

    void OnDestroy()
    {
        if (subscribed && Inventory.Instance != null)
            Inventory.Instance.OnChanged -= UpdatePopulation;
    }

    void HandleTerrainReady(Vector3 spawnPoint)
    {
        terrainReady = true;
        UpdatePopulation();
    }

    // Cuánto aprieta el planeta según lo lleno que esté el catálogo.
    //
    // Sube con las fichas recogidas, que ahora es lo mismo que decir "según lo cerca que
    // estés de poder irte": como no se despega sin el catálogo entero, el jugador siempre
    // recorre la curva completa. Antes podía cortar por lo sano a la mitad y no llegaba a
    // ver el tramo duro nunca.
    void AplicarPresion(int collected)
    {
        int total = Inventory.Instance != null ? Inventory.Instance.Total : 0;
        float avance = total > 0 ? Mathf.Clamp01(collected / (float)total) : 0f;

        float oido = 1f + (Tramo - 1) * hearingPerTramo + avance * hearingAtFullCatalogue;
        float caza = 1f + avance * huntSpeedAtFullCatalogue;

        for (int i = 0; i < spiders.Count; i++)
        {
            if (spiders[i] == null) continue;
            spiders[i].HearingScale = oido;
            spiders[i].HuntScale = caza;
        }

        // Al levantar la última pieza, todas saben por dónde ibas.
        //
        // Es el remate de la bajada y hace falta que exista: con el catálogo obligatorio,
        // el final de cada descenso es siempre la vuelta al módulo cargado con todo, y sin
        // esto esa vuelta es el tramo más aburrido justo cuando debería ser el peor.
        //
        // Una sola vez, no en bucle: se les manda la última posición conocida, no un
        // seguimiento. Se les agota la alerta como con cualquier ruido, así que se puede
        // despistarlas escondiéndose. No es una sentencia, es una ventaja perdida.
        if (!avisoDeCierre && Inventory.Instance != null && Inventory.Instance.IsComplete)
        {
            avisoDeCierre = true;

            if (player != null)
                foreach (var spider in spiders)
                    if (spider != null) spider.Alert(player.position);
        }
    }

    void UpdatePopulation()
    {
        if (!terrainReady) return;

        spiders.RemoveAll(s => s == null);

        int collected = Inventory.Instance != null ? Inventory.Instance.Collected.Count : 0;
        int target = baseCount;
        if (itemsPerExtraSpider > 0) target += collected / itemsPerExtraSpider;

        // El tramo del cuerpo también cuenta, y hasta ahora no contaba: el último cuerpo
        // de la ruta tenía exactamente la misma amenaza base que el primero. El lore
        // escalaba en tres tramos y la superficie no escalaba nada, así que la segunda
        // mitad de la ruta se sentía más fácil que la primera solo porque ya sabías jugar.
        target += (Tramo - 1) * spidersPerTramo;

        // La presión no la lleva el número, y esto es lo importante de toda la curva.
        //
        // El mapa mide 243 unidades de lado. Nueve arañas ahí están tan repartidas que
        // pasar de cinco a nueve no se nota: sigues sin cruzarte con casi ninguna. Subir
        // población en un mapa así no sube la tensión, solo el coste de simularlo.
        //
        // Lo que SÍ se nota es cuánto oyen. El radio de audición decide si una araña se
        // entera de que existes, y subirlo hace que el mismo mapa se cierre a tu
        // alrededor sin que aparezca nada nuevo. Es la diferencia entre más enemigos y
        // enemigos peores, y para un bicho ciego lo segundo es lo que cuenta.
        AplicarPresion(collected);

        // Bajar sin haber identificado el cuerpo se paga acá. Es lo único que hace que
        // fallar el estudio de PARALAX signifique algo en la superficie.
        target = Mathf.RoundToInt(target * Expedition.DescentDifficulty);
        target = Mathf.Min(target, maxSpiders);

        int missing = target - spiders.Count;
        for (int i = 0; i < missing; i++)
        {
            if (!TrySpawn()) break;
        }
    }

    bool TrySpawn()
    {
        var walkable = generator.WalkableCellPositions;
        if (walkable.Count == 0) return false;

        for (int attempt = 0; attempt < 240; attempt++)
        {
            Vector3 candidate = walkable[Random.Range(0, walkable.Count)];

            if (Vector3.Distance(candidate, generator.GroundSpawnPoint) < minDistanceFromSpawn) continue;
            if (player != null && Vector3.Distance(candidate, player.position) < minDistanceFromPlayer) continue;
            if (IsOnScreen(candidate)) continue;

            Spawn(candidate);
            return true;
        }

        // Sin sitio válido ahora mismo. No se fuerza: es preferible que falte una
        // araña a que el jugador la vea aparecer de la nada.
        return false;
    }

    bool IsOnScreen(Vector3 worldPosition)
    {
        if (viewCamera == null) return false;

        Vector3 v = viewCamera.WorldToViewportPoint(worldPosition + Vector3.up * 1.75f);
        if (v.z <= 0f) return false;

        return v.x > -viewportMargin && v.x < 1f + viewportMargin
            && v.y > -viewportMargin && v.y < 1f + viewportMargin;
    }

    void Spawn(Vector3 groundPosition)
    {
        // Se crea desactivado a propósito: AddComponent dispara Awake al instante,
        // y Awake es quien arma el cuerpo y las patas. Si el material se asignara
        // después, la araña ya estaría construida con el de respaldo.
        var go = new GameObject("Araña");
        go.SetActive(false);
        go.transform.SetParent(transform);
        go.transform.position = groundPosition + Vector3.up * 1.75f;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        var spider = go.AddComponent<SpiderCreature>();
        spider.SetMaterial(spiderMaterial);

        // La voz va antes de activar el objeto, igual que el material: su Awake monta la
        // fuente de audio y conviene que ya tenga los clips cuando lo haga.
        go.AddComponent<SpiderVoice>().SetClips(chatterClips);

        // Además de haber más, oyen más lejos. Solo con el número, un tramo avanzado en
        // un mapa grande puede sentirse igual de vacío: son más bichos repartidos por
        // más terreno. El oído es lo que hace que el sitio se sienta distinto.
        spider.HearingScale = 1f + (Tramo - 1) * hearingPerTramo;

        go.SetActive(true);

        spiders.Add(spider);
        Debug.Log($"Arañas activas: {spiders.Count}.");
    }
}
