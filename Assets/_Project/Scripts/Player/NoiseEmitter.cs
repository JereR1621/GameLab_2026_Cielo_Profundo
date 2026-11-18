using UnityEngine;

// Cuánto ruido hace el jugador, expresado como el radio dentro del cual se lo escucha.
// Es lo que la araña consulta para saber si te oyó.
[RequireComponent(typeof(CharacterController))]
public class NoiseEmitter : MonoBehaviour
{
    public static NoiseEmitter Instance { get; private set; }

    [SerializeField] PlayerController playerController;
    [SerializeField] RadarUI radar;

    [Header("Radios de audición, en unidades del mundo")]
    [SerializeField] float idleNoise = 3f;
    [SerializeField] float walkNoise = 22f;
    [Tooltip("Cuánto reduce el ruido de los pasos ir agachado.")]
    [SerializeField, Range(0f, 1f)] float crouchMultiplier = 0.22f;

    [Tooltip("Correr te delata muchísimo. Es la contrapartida: escapas más rápido, pero le dices exactamente dónde estás.")]
    [SerializeField] float runNoiseMultiplier = 1.9f;

    [Tooltip("El radar te delata aunque estés agachado y quieto: encenderlo es una decisión, no un gesto gratis.")]
    [SerializeField] float radarNoise = 34f;

    [Tooltip("Cuánto multiplica el ruido de pasos la suela adherente comprada en la nave. Vale para un descenso, y NO afecta al radar: encenderlo sigue delatándote igual.")]
    [SerializeField, Range(0.1f, 1f)] float quietStepMultiplier = 0.55f;

    [Tooltip("Radio de audición estando escondido en un cráter. Muy bajo, pero no cero.")]
    [SerializeField] float hiddenNoise = 1.5f;

    [SerializeField] float smoothing = 5f;

    CharacterController controller;

    public float NoiseRadius { get; private set; }

    // El máximo que puede alcanzar el radio con esta configuración. Lo usa el medidor
    // del HUD para tener contra qué comparar: una barra sin tope no dice nada.
    public float MaxNoiseRadius => Mathf.Max(walkNoise * runNoiseMultiplier, radarNoise);

    // Lo que hacen los PASOS, de 0 a 1, sin contar el radar. Lo usan las pisadas para
    // sonar exactamente igual de fuerte que lo que la araña está oyendo: así el jugador
    // aprende el sistema de ruido de oído y no leyendo la barra de la esquina.
    //
    // Se separa del radar a propósito. El radar es el ruido más alto del juego, pero no
    // sale de tus pies: si entrara en esta cuenta, desplegarlo te haría pisar más fuerte,
    // que no tiene ningún sentido.
    public float StepLoudness01 { get; private set; }

    // Tope de referencia medido ANTES de aplicar la suela adherente. Normalizando contra
    // el tope ya rebajado, comprar la suela no cambiaría nada: el numerador y el
    // denominador bajarían juntos y las pisadas sonarían igual de fuertes. Que se oiga
    // la diferencia es justamente lo que el jugador pagó.
    float maxSinPreparacion;

    // Desde dónde sale tu ruido. Importa para la oclusión: agachado el origen baja, así
    // que un muro bajo alcanza para tapar la línea hacia la araña. Agacharse pasa a
    // servir para dos cosas —hacer menos ruido y esconder el que haces— en vez de una.
    public Vector3 SoundOrigin
    {
        get
        {
            bool crouching = playerController != null && playerController.IsCrouching;
            // Escondido en un cráter el origen baja casi al suelo: es exactamente lo que
            // permite que el labio, que es bajo, tape la línea hacia la araña.
            if (PlayerHiding.Instance != null && PlayerHiding.Instance.IsHiding)
                return transform.position + Vector3.up * 0.35f;

            return transform.position + Vector3.up * (crouching ? 0.55f : 1.3f);
        }
    }

    void Awake()
    {
        Instance = this;

        // Suela adherente: comprada en la nave, vale para ESTE descenso. Baja el ruido de
        // los pasos sin tocar la velocidad, así que te hace más sigiloso pero no más
        // rápido ni más fuerte. No cambia lo que puedes hacer, cambia el margen que
        // tienes para hacerlo.
        maxSinPreparacion = Mathf.Max(walkNoise * runNoiseMultiplier, radarNoise);

        if (Expedition.QuietStepReady)
        {
            walkNoise *= quietStepMultiplier;
            runNoiseMultiplier *= quietStepMultiplier;
        }
        controller = GetComponent<CharacterController>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        Vector3 v = controller.velocity;
        float planarSpeed = new Vector2(v.x, v.z).magnitude;

        float maxSpeed = playerController != null ? playerController.MaxPlanarSpeed : 6f;
        float footNoise = Mathf.Lerp(idleNoise, walkNoise, Mathf.Clamp01(planarSpeed / maxSpeed));

        if (playerController != null)
        {
            if (playerController.IsCrouching) footNoise *= crouchMultiplier;
            else if (playerController.IsRunning) footNoise *= runNoiseMultiplier;
        }

        StepLoudness01 = maxSinPreparacion > 0f ? Mathf.Clamp01(footNoise / maxSinPreparacion) : 0f;

        // Escondido en un cráter el ruido se derrumba. No es cero: quedarte
        // quieto ahí no te borra, solo te vuelve muy difícil de ubicar.
        if (PlayerHiding.Instance != null && PlayerHiding.Instance.IsHiding)
        {
            NoiseRadius = Mathf.Lerp(NoiseRadius, hiddenNoise, Time.deltaTime * smoothing);
            return;
        }

        // El radar no se suma al ruido de los pasos: lo pisa. Agacharse no te salva
        // de tenerlo encendido, que es justamente el dilema que queremos.
        float target = footNoise;
        if (radar != null && radar.IsDeployed)
            target = Mathf.Max(target, radarNoise);

        NoiseRadius = Mathf.Lerp(NoiseRadius, target, Time.deltaTime * smoothing);
    }
}
