using UnityEngine;

// La voz de la araña: el parloteo por el que sabes que hay una cerca sin verla.
//
// Es el sonido más importante del juego. La mecánica entera es que ella oye y tú no la
// ves; hasta ahora eso solo funcionaba en un sentido, porque tampoco podías oírla a ella.
// Sin esto, encontrarse una es siempre una sorpresa, y una sorpresa que no pudiste
// prevenir no da miedo: da rabia.
//
// Dos decisiones que definen cómo suena:
//
// · A RÁFAGAS, no en bucle. El clip original son cuatro segundos y medio de parloteo
//   continuo; encadenado y con nueve arañas en el mapa quedaría un zumbido de fondo del
//   que dejas de enterarte. Cortado en ráfagas de medio segundo con silencios entre
//   medio, cada una es un aviso: «ahí está… y ahí está otra vez».
//
// · SIN REVERB, al revés que las pisadas y la alarma. Acá la información es la DIRECCIÓN,
//   y el reverb la emborrona: las reflexiones llegan de todos lados y el oído pierde el
//   ángulo. En este sonido la claridad vale más que el espacio.
[RequireComponent(typeof(SpiderCreature))]
public class SpiderVoice : MonoBehaviour
{
    [Tooltip("Ráfagas de parloteo. Se elige una al azar cada vez, para que no se note el ciclo.")]
    [SerializeField] AudioClip[] clips;

    [SerializeField, Range(0f, 1f)] float volume = 0.7f;

    [Tooltip("A partir de aquí ya no se oye. Algo mayor que el radio con que ella te oye a ti: así puedes detectarla antes de que ella te detecte, que es lo que hace justo el sigilo.")]
    [SerializeField] float audibleRange = 48f;

    [Header("Merodeando")]
    [SerializeField] Vector2 calmInterval = new Vector2(3.4f, 6.5f);
    [SerializeField] Vector2 calmPitch = new Vector2(0.88f, 1.02f);

    [Header("Persiguiendo")]
    [Tooltip("Mucho más seguido, y más agudo. Es lo que te dice que te oyó, y es la información que decide si te escondes o sigues.")]
    [SerializeField] Vector2 huntInterval = new Vector2(0.9f, 1.9f);

    [SerializeField] Vector2 huntPitch = new Vector2(1.08f, 1.26f);

    SpiderCreature spider;
    AudioSource source;
    float next;

    void Awake()
    {
        spider = GetComponent<SpiderCreature>();

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.dopplerLevel = 0f;

        // Totalmente espacializado: de este sonido lo que importa es de dónde viene.
        source.spatialBlend = 1f;

        // Caída lineal. Con la logarítmica de Unity el grueso del volumen se pierde en
        // los primeros metros y el resto es un hilo inaudible, así que el aviso llegaría
        // cuando ya la tienes encima.
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 5f;
        source.maxDistance = audibleRange;

        // Repartidas al arrancar. Sin esto todas las arañas del mapa parlotearían a la
        // vez en el primer segundo del descenso y después se callarían juntas.
        next = Random.Range(0f, calmInterval.y);
    }

    void Update()
    {
        if (clips == null || clips.Length == 0) return;
        if (spider == null || spider.FrozenByCinematic) return;

        next -= Time.deltaTime;
        if (next > 0f) return;

        bool cazando = spider.IsHunting;

        var intervalo = cazando ? huntInterval : calmInterval;
        var tono = cazando ? huntPitch : calmPitch;

        next = Random.Range(intervalo.x, intervalo.y);

        var clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        source.pitch = Random.Range(tono.x, tono.y);
        source.PlayOneShot(clip, volume * GameSettings.EffectsVolume);
    }

    // La pone el generador, porque las arañas se crean en tiempo de ejecución y no hay
    // dónde arrastrar los clips desde el editor.
    public void SetClips(AudioClip[] nuevos) => clips = nuevos;
}
