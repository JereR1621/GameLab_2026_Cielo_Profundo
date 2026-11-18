using UnityEngine;

// Música de fondo del menú. Entra desde silencio y se queda en bucle.
//
// El volumen final es bajo a propósito: esto es ambiente, no una canción que se
// escucha. Tiene que quedar por debajo de cualquier efecto que venga después.
//
// El bucle es directo, sin cruce, porque el archivo ya viene cortado en el punto donde
// la música vuelve a decir lo que dice al empezar — eso lo resolvió la herramienta de
// Tools/audio, no hace falta arreglarlo otra vez en tiempo de ejecución.
public class AmbientMusic : MonoBehaviour
{
    [SerializeField] AudioClip clip;

    [Tooltip("Volumen de ambiente, antes de aplicar el ajuste del jugador. Bajo: no tiene que competir con nada.")]
    [SerializeField, Range(0f, 1f)] float ambientVolume = 0.38f;

    [Tooltip("Segundos que tarda en subir desde silencio.")]
    [SerializeField] float fadeInSeconds = 4f;

    [Tooltip("Arranca sola al cargar la escena. En el menú sí; en las fichas de inicio la dispara la propia ficha, porque solo suena mientras están abiertas.")]
    [SerializeField] bool autoPlay = true;

    [Tooltip("Segundos de espera antes de empezar. En la superficie sirve para no pisar el aterrizaje.")]
    [SerializeField] float startDelay = 0f;

    // Silencio entre pasada y pasada. En cero, bucle continuo como siempre.
    //
    // En la superficie es imprescindible y no por gusto: la mecánica central es oír, y
    // música sonando sin parar tapa las pisadas y a la araña justo cuando hay que
    // distinguirlas. Con pausas, la tensión respira y los silencios se vuelven parte del
    // juego en vez de un hueco.
    //
    // Y encaja con este tema en concreto, que termina desvanecido: encadenado sin pausa
    // se apagaría del todo y volvería a entrar de golpe, que es el salto que se nota.
    [SerializeField] float loopGapSeconds = 0f;

    AudioSource source;
    float fade;          // 0 a 1, la envolvente de entrada y salida
    float fadeTarget = 1f;
    float fadeSpeed;
    float gapLeft;
    bool started;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        // Con pausa entre pasadas el bucle lo lleva este componente, no el AudioSource.
        source.loop = loopGapSeconds <= 0.01f;
        source.playOnAwake = false;

        // 2D: es música, no una fuente en el espacio. Con spatialBlend en 3D quedaría
        // atada a la posición del objeto y se oiría de un lado.
        source.spatialBlend = 0f;
        source.volume = 0f;

        fade = 0f;
        fadeSpeed = 1f / Mathf.Max(0.01f, fadeInSeconds);
    }

    void Start()
    {
        if (!autoPlay) return;
        if (startDelay <= 0f) Play();
        else Invoke(nameof(Play), startDelay);
    }

    public void Play()
    {
        if (clip == null)
        {
            Debug.LogWarning("AmbientMusic: sin clip asignado.");
            return;
        }

        if (source.isPlaying) return;

        GameSettings.EnsureApplied();
        fadeTarget = 1f;
        fadeSpeed = 1f / Mathf.Max(0.01f, fadeInSeconds);
        started = true;
        gapLeft = 0f;
        source.Play();
    }

    void Update()
    {
        if (source == null) return;

        RepetirConPausa();

        fade = Mathf.MoveTowards(fade, fadeTarget, fadeSpeed * Time.deltaTime);

        // El ajuste del jugador se lee cada cuadro, no una sola vez: así mover el
        // control de volumen en opciones se oye mientras lo mueves.
        source.volume = fade * ambientVolume * GameSettings.MusicVolume;
    }

    // Vuelve a lanzar el tema tras el silencio pactado.
    //
    // Solo cuando hay pausa: sin ella el AudioSource encadena solo, que es lo que quiere
    // el menú. Y solo si no está apagándose, para que un FadeOut no reviva el tema justo
    // cuando se le acaba de pedir que se calle.
    void RepetirConPausa()
    {
        if (!started || source.loop || clip == null) return;
        if (fadeTarget <= 0f) return;
        if (source.isPlaying) return;

        gapLeft -= Time.deltaTime;
        if (gapLeft > 0f) return;

        gapLeft = loopGapSeconds;
        source.Play();
    }

    // La usa la salida del menú: la música se va con la imagen. Sin esto se cortaría en
    // seco al cargar la nave, justo cuando la pantalla está en negro.
    public void FadeOut(float seconds)
    {
        fadeTarget = 0f;
        fadeSpeed = 1f / Mathf.Max(0.01f, seconds);
    }
}
