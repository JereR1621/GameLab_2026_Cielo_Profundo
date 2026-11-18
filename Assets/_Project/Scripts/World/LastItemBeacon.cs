using UnityEngine;

// Localizador de la última pieza.
//
// Con la recogida completa obligatoria, el peor momento del descenso es el final: llevas
// nueve de diez y te queda dar vueltas por un mapa de 243 unidades buscando la que falta.
// Eso no es tensión, es un registro de habitaciones. Y encima es cuando más arañas hay.
//
// Cuando queda una sola, la pieza se delata: enciende una luz y emite un pitido cada
// cierto tiempo. El truco está en que lo que informa NO es el volumen, es la CADENCIA —
// lejos pita cada dos segundos y medio, encima casi seguido. Es el detector de metales de
// toda la vida, y funciona porque el oído distingue ritmos mucho mejor que distancias.
//
// El pitido es la misma alarma del módulo pero aguda y bajita. Reusar el sonido no es
// pereza: emparenta las dos señales —"esto es información del traje"— y a la vez las
// distingue sin confusión posible, porque una es grave y fuerte y la otra aguda y floja.
public class LastItemBeacon : MonoBehaviour
{
    [SerializeField] Transform player;

    [Tooltip("El mismo clip que la alarma del módulo, reproducido agudo y flojo.")]
    [SerializeField] AudioClip pingClip;

    [Header("Pitido")]
    [Tooltip("Tono. Muy por encima de 1: es lo que lo separa de la alarma del módulo, que suena a su tono normal.")]
    [SerializeField] float pitch = 1.8f;

    [Tooltip("Volumen. Bajo a propósito: tiene que oírse sin tapar los pasos ni a la araña, que siguen siendo la información que te mantiene vivo.")]
    [SerializeField, Range(0f, 1f)] float volume = 0.32f;

    [Tooltip("Segundos entre pitidos estando lejos.")]
    [SerializeField] float slowestInterval = 2.5f;

    [Tooltip("Segundos entre pitidos estando encima.")]
    [SerializeField] float fastestInterval = 0.28f;

    [Tooltip("A partir de esta distancia el ritmo ya no baja más. Es el alcance útil del localizador.")]
    [SerializeField] float audibleRange = 190f;

    [Tooltip("Por debajo de esta distancia el ritmo ya no sube más: es cuando lo tienes delante.")]
    [SerializeField] float closeRange = 6f;

    [Header("Luz")]
    [SerializeField] Color beaconColor = new Color(0.62f, 0.92f, 1f);
    [SerializeField] float lightRange = 22f;
    [SerializeField] float lightIntensity = 4.5f;

    [Tooltip("Alto de la señal visible. Corta a propósito: la del módulo mide 110 y es naranja, así que no hay forma de confundirlas.")]
    [SerializeField] float beamHeight = 15f;

    [SerializeField] Material beamMaterial;

    Collectible target;
    Light glow;
    Transform beam;
    AudioSource source;
    float nextPing;

    void Awake()
    {
        BuildSignal();
        BuildAudio();
        Mostrar(false);
    }

    void Update()
    {
        target = Localizar();

        if (target == null)
        {
            Mostrar(false);
            return;
        }

        // La señal se coloca sobre la pieza en cada cuadro. Las fichas no se mueven, pero
        // si algún día flotan o giran, esto las sigue sin tocar nada.
        transform.position = target.transform.position;
        Mostrar(true);

        if (player == null) return;

        Vector3 delta = player.position - target.transform.position;
        delta.y = 0f;
        float distancia = delta.magnitude;

        // Cerca del 0 estando encima, cerca del 1 estando en la otra punta del mapa.
        float lejania = Mathf.Clamp01(Mathf.InverseLerp(closeRange, audibleRange, distancia));

        Latir(lejania);
        Pitar(lejania);
    }

    Collectible Localizar()
    {
        var inv = Inventory.Instance;
        if (inv == null || inv.Total <= 0) return null;

        // La condición se mira sobre el inventario y no sobre cuántas quedan en la lista,
        // porque durante el reparto inicial la lista pasa por tener una sola pieza y el
        // localizador se encendería al aterrizar.
        if (inv.Collected.Count != inv.Total - 1) return null;

        for (int i = 0; i < Collectible.All.Count; i++)
        {
            var c = Collectible.All[i];
            if (c != null && c.gameObject.activeInHierarchy) return c;
        }

        return null;
    }

    void Latir(float lejania)
    {
        // La luz también late más rápido de cerca, al mismo ritmo que el pitido: las dos
        // señales dicen lo mismo por dos vías, y de lejos la luz sola ya orienta.
        float ritmo = Mathf.Lerp(3.4f, 0.8f, lejania);
        float onda = 0.5f + 0.5f * Mathf.Sin(Time.time * ritmo * Mathf.PI * 2f);

        if (glow != null) glow.intensity = lightIntensity * (0.35f + 0.65f * onda);
        if (beam != null)
        {
            float ancho = 0.5f * (0.7f + 0.5f * onda);
            beam.localScale = new Vector3(ancho, beamHeight * 0.5f, ancho);
        }
    }

    void Pitar(float lejania)
    {
        if (source == null || pingClip == null) return;

        nextPing -= Time.deltaTime;
        if (nextPing > 0f) return;

        nextPing = Mathf.Lerp(fastestInterval, slowestInterval, lejania);

        // El tono sube un poco al acercarse, además de la cadencia. Es el remate que hace
        // que "caliente" se note aunque no estés contando los intervalos.
        source.pitch = pitch + (1f - lejania) * 0.25f;
        source.PlayOneShot(pingClip, volume * GameSettings.EffectsVolume);
    }

    void Mostrar(bool visible)
    {
        if (glow != null) glow.enabled = visible;
        if (beam != null && beam.gameObject.activeSelf != visible)
            beam.gameObject.SetActive(visible);
    }

    void BuildSignal()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "SeñalUltima";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, beamHeight * 0.5f, 0f);
        go.transform.localScale = new Vector3(0.5f, beamHeight * 0.5f, 0.5f);

        if (beamMaterial != null) go.GetComponent<Renderer>().sharedMaterial = beamMaterial;

        var renderer = go.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        beam = go.transform;

        var luzGO = new GameObject("LuzUltima");
        luzGO.transform.SetParent(transform, false);
        luzGO.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        glow = luzGO.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = beaconColor;
        glow.range = lightRange;
        glow.intensity = lightIntensity;
        glow.shadows = LightShadows.None;
    }

    void BuildAudio()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.dopplerLevel = 0f;

        // Espacializado para que se oiga de qué lado viene, pero con caída lineal hasta
        // el otro extremo del mapa: con la curva logarítmica por defecto se apagaría a
        // treinta unidades y el localizador solo serviría cuando ya lo has encontrado.
        source.spatialBlend = 0.75f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 4f;
        source.maxDistance = audibleRange;
    }
}
