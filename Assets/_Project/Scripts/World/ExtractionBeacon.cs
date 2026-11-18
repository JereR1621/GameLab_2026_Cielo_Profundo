using UnityEngine;

// La baliza del módulo de ascenso: se enciende al cerrarse el catálogo.
//
// Con la recogida completa obligatoria, el final de cada bajada es siempre la vuelta al
// módulo con todo encima, y en un mapa de 243 unidades con bruma cerrada esa vuelta era
// sobre todo un problema de orientación. Buscar el aparcamiento no da miedo, da pereza.
//
// La baliza convierte ese tramo en lo que tiene que ser: sabes exactamente adónde vas,
// lo ves desde cualquier parte, y el problema pasa a ser llegar vivo. Que es el problema
// interesante.
//
// Lo que se ve de lejos NO es la luz, y esto importa: una luz puntual se pierde a treinta
// unidades por mucha intensidad que le pongas. Lo que se ve es la columna — una barra
// alta y brillante que sale del módulo y que la bruma no tapa porque es geometría, no
// iluminación. La luz solo sirve para lo de cerca.
public class ExtractionBeacon : MonoBehaviour
{
    [SerializeField] Material beamMaterial;

    [Tooltip("Alto de la columna. Enorme a propósito: tiene que asomar por encima de los cerros desde el otro lado del mapa.")]
    [SerializeField] float beamHeight = 110f;

    [SerializeField] float beamWidth = 1.3f;

    [Tooltip("Desde dónde arranca la columna, sobre el módulo.")]
    [SerializeField] float beamBase = 6f;

    [Tooltip("Alcance de la luz de sitio. Solo para cuando ya llegaste: la que orienta de lejos es la columna.")]
    [SerializeField] float lightRange = 34f;

    [SerializeField] float lightIntensity = 6f;
    [SerializeField] Color beaconColor = new Color(0.99f, 0.62f, 0.26f);

    [Header("Alarma")]
    [Tooltip("Suena una vez al cerrarse el catálogo.")]
    [SerializeField] AudioClip alarmClip;

    [SerializeField, Range(0f, 1f)] float alarmVolume = 0.85f;

    [Tooltip("Sala que simula el reverb. Mountains da reflexiones largas y separadas, que es lo que devuelve un descampado con cerros; Cave o Quarry sonarían a interior cerrado.")]
    [SerializeField] AudioReverbPreset alarmReverb = AudioReverbPreset.Mountains;

    Transform beam;
    Light glow;
    AudioSource alarm;

    [Tooltip("Latidos por segundo. Lento: una baliza de emergencia pulsa despacio, y rápido parecería una alarma de incendios.")]
    [SerializeField] float pulsesPerSecond = 0.55f;

    [Tooltip("Cadencia del parpadeo. A 12 fps, igual que el resto del juego.")]
    [SerializeField] float frameRate = 12f;

    bool encendida;
    int lastFrame = -1;

    public bool IsLit => encendida;

    void Awake()
    {
        BuildBeam();
        BuildLight();
        BuildAlarm();
        Apagar();
    }

    void Update()
    {
        if (!encendida)
        {
            // Se consulta el inventario en vez de esperar un aviso de nadie. Es una
            // comprobación de dos campos por cuadro y evita depender de en qué orden
            // arrancan los componentes, que en este proyecto ya ha costado varios fallos.
            if (Inventory.Instance == null || !Inventory.Instance.IsComplete) return;

            Encender();
            return;
        }

        // El latido va escalonado a 12 fps como todo lo demás. Interpolado suave se vería
        // moderno y se saldría del acabado del resto del juego.
        int frame = Mathf.FloorToInt(Time.time * frameRate);
        if (frame == lastFrame) return;
        lastFrame = frame;

        float onda = 0.5f + 0.5f * Mathf.Sin(Time.time * pulsesPerSecond * Mathf.PI * 2f);

        if (glow != null) glow.intensity = lightIntensity * (0.45f + 0.55f * onda);

        if (beam != null)
        {
            // Late en ancho, no en alto: cambiándole el alto parecería que sube y baja,
            // y lo que tiene que parecer es que respira.
            float ancho = beamWidth * (0.78f + 0.42f * onda);
            beam.localScale = new Vector3(ancho, beamHeight * 0.5f, ancho);
        }
    }

    void Encender()
    {
        encendida = true;
        if (beam != null) beam.gameObject.SetActive(true);
        if (glow != null) glow.enabled = true;

        if (alarm != null && alarmClip != null)
            alarm.PlayOneShot(alarmClip, alarmVolume * GameSettings.EffectsVolume);
    }

    void Apagar()
    {
        encendida = false;
        if (beam != null) beam.gameObject.SetActive(false);
        if (glow != null) glow.enabled = false;
    }

    void BuildBeam()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Columna";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);

        // El cilindro de Unity mide dos unidades de alto, de ahí el medio en la escala.
        go.transform.localPosition = new Vector3(0f, beamBase + beamHeight * 0.5f, 0f);
        go.transform.localScale = new Vector3(beamWidth, beamHeight * 0.5f, beamWidth);

        if (beamMaterial != null) go.GetComponent<Renderer>().sharedMaterial = beamMaterial;

        // Sin sombras: una columna de cien unidades proyectando sombra pintaría una
        // franja negra por medio planeta.
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        beam = go.transform;
    }

    void BuildLight()
    {
        var go = new GameObject("LuzBaliza");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, beamBase, 0f);

        glow = go.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = beaconColor;
        glow.range = lightRange;
        glow.intensity = lightIntensity;
        glow.shadows = LightShadows.None;
    }

    // La alarma.
    //
    // Casi en 2D aunque el objeto esté en el módulo, y es a propósito: no es la bocina
    // del vehículo, es el aviso del traje. Espacializada de verdad no se oiría, porque
    // cuando se dispara sueles estar a cien unidades de allí — y un aviso que solo suena
    // si ya estás al lado no avisa de nada.
    //
    // El reverb va acá, sobre la fuente, y no en una zona de reverb del mundo: la zona
    // afectaría a todo lo que suene dentro de ella y habría que colocarla y dimensionarla
    // por cuerpo. Un filtro en la fuente elige exactamente qué sonidos se sitúan en el
    // espacio y cuáles no.
    void BuildAlarm()
    {
        var go = new GameObject("Alarma");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        alarm = go.AddComponent<AudioSource>();
        alarm.playOnAwake = false;
        alarm.loop = false;
        alarm.spatialBlend = 0.15f;
        alarm.dopplerLevel = 0f;

        var reverb = go.AddComponent<AudioReverbFilter>();
        reverb.reverbPreset = alarmReverb;
    }
}
