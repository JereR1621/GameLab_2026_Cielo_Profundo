using UnityEngine;

// Decide a qué cuerpo de la ruta corresponde esta partida y se lo comunica a quien
// lo necesite. Trabaja en Awake porque el generador de terreno genera en su Start
// y necesita la semilla ya fijada para entonces.
public class PlanetSession : MonoBehaviour
{
    public static PlanetSession Instance { get; private set; }

    [SerializeField] NoiseTerrainGenerator generator;
    [Tooltip("Ignora la ruta y usa siempre una semilla nueva. Sirve para probar terreno, no para jugar.")]
    [SerializeField] bool freeRoamTesting;

    public BodyRecord Body { get; private set; }
    public int Tramo => Body != null ? Body.tramo : 1;

    void Awake()
    {
        Instance = this;
        Body = RouteDatabase.Current;

        if (Body == null)
        {
            Debug.LogWarning("La ruta está vacía: se juega con semilla al azar.");
            return;
        }

        if (!freeRoamTesting && generator != null)
        {
            generator.OverrideSeed(Body.semilla);

            if (!string.IsNullOrEmpty(Body.colorTerreno))
                generator.OverrideBaseColor(Body.ParseColor(Body.colorTerreno, Color.gray));
        }

        Debug.Log($"Cuerpo asignado: {Body.nombre} — {Body.clase} ({Body.tipo}), " +
                  $"tramo {Body.tramo}, semilla {Body.semilla}.");
    }

    // Las preparaciones se gastan acá, en Start y no en Awake: quienes las leen —el
    // controlador para el aguante y el emisor de ruido para la suela— lo hacen en SUS
    // Awake, y el orden entre componentes no está garantizado. Todos los Awake corren
    // antes que cualquier Start, así que acá ya las consumieron con seguridad.
    void Start()
    {
        Expedition.ConsumePreparations();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
