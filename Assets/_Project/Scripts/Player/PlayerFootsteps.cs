using UnityEngine;

// Las pisadas del jugador.
//
// En un juego cuya mecánica central es que el bicho te oye, esto no es ambientación: es
// la única forma de que sepas cuánto ruido estás haciendo sin mirar la barra del HUD.
// Por eso el volumen no sale de si caminas o corres, sale del MISMO número que la araña
// usa para oírte. Agacharte suena bajo porque de verdad estás siendo silencioso, y la
// suela adherente que compraste en la nave se nota al primer paso.
//
// Los clips son dos, uno por pie, y alternan. Con un solo clip repetido el oído detecta
// el bucle en tres pasos y deja de creérselo.
[RequireComponent(typeof(AudioSource))]
public class PlayerFootsteps : MonoBehaviour
{
    [SerializeField] PlayerAnimator playerAnimator;

    [Tooltip("Pie izquierdo.")]
    [SerializeField] AudioClip left;

    [Tooltip("Pie derecho.")]
    [SerializeField] AudioClip right;

    [Tooltip("Volumen del paso más fuerte posible, antes del ajuste de efectos del jugador.")]
    [SerializeField, Range(0f, 1f)] float maxVolume = 0.85f;

    [Tooltip("Volumen del paso más silencioso. No es cero: agachado se oye poco, pero se oye, y eso es lo que te dice que sigues haciendo ruido.")]
    [SerializeField, Range(0f, 1f)] float minVolume = 0.12f;

    [Tooltip("Variación de tono a cada paso. Sin esto se nota que son dos archivos repetidos.")]
    [SerializeField] Vector2 pitchRange = new Vector2(0.93f, 1.07f);

    [Tooltip("Cuánto más grave suena el paso al correr. Pisar con fuerza baja el tono, no lo sube.")]
    [SerializeField] float runPitchDrop = 0.06f;

    [Tooltip("Sala del reverb. Mountains da reflexiones largas y separadas, que es lo que devuelve un descampado con cerros.")]
    [SerializeField] AudioReverbPreset stepReverb = AudioReverbPreset.Mountains;

    AudioSource source;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;

        // Casi 2D. Son tus propios pies: en tercera persona, espacializarlos del todo
        // los aleja de ti cuando la cámara se separa, y el dato que tienen que dar
        // —cuánto ruido haces— no depende de dónde esté la cámara.
        source.spatialBlend = 0.25f;
        source.dopplerLevel = 0f;

        // Reverb en las pisadas, que es donde de verdad se nota el tamaño del sitio.
        // La música va seca a propósito: es banda sonora, no algo que suene dentro del
        // planeta, y bañarla en reverb solo la emborrona. El espacio lo cuentan los
        // sonidos que SÍ nacen ahí — tus pisadas y la alarma del módulo.
        var reverb = gameObject.AddComponent<AudioReverbFilter>();
        reverb.reverbPreset = stepReverb;
    }

    // En OnEnable y no en Awake: el animador puede construirse después, y suscribirse en
    // Awake dependería de un orden que Unity no garantiza. Es el mismo cuidado que llevan
    // el módulo de ascenso y el generador de terreno.
    void OnEnable()
    {
        if (playerAnimator == null) playerAnimator = GetComponent<PlayerAnimator>();
        if (playerAnimator != null) playerAnimator.OnFootfall += Pisar;
    }

    void OnDisable()
    {
        if (playerAnimator != null) playerAnimator.OnFootfall -= Pisar;
    }

    void Pisar(bool derecho)
    {
        var clip = derecho ? right : left;
        if (clip == null || source == null) return;

        float fuerza = NoiseEmitter.Instance != null ? NoiseEmitter.Instance.StepLoudness01 : 0.5f;

        float volumen = Mathf.Lerp(minVolume, maxVolume, fuerza) * GameSettings.EffectsVolume;
        if (volumen <= 0.001f) return;

        // El tono baja con la fuerza del paso además de variar al azar: un golpe fuerte
        // en piedra suena más grave, y sin eso correr sonaría igual que caminar pero más
        // seguido.
        source.pitch = Random.Range(pitchRange.x, pitchRange.y) - fuerza * runPitchDrop;

        // PlayOneShot y no Play: a cadencia de carrera un paso empieza antes de que el
        // anterior se haya apagado, y con Play el nuevo cortaría al viejo en seco.
        source.PlayOneShot(clip, volumen);
    }
}
