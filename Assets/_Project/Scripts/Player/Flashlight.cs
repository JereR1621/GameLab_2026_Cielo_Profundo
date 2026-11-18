using UnityEngine;

// Lámpara de casco. Se prende y se apaga con F.
//
// Está pensada para NO deshacer la oscuridad. El alcance es corto a propósito: con la
// niebla de cualquier cuerpo de la ruta, la luz se apaga sola bastante antes del
// horizonte. Ilumina dónde pisas y un poco más allá; no sirve para explorar de lejos.
//
// Apunta con la cámara y no con el cuerpo. PlayerController solo gira al personaje
// cuando hay entrada de movimiento, así que una luz atada a su rotación se quedaría
// clavada hacia donde caminaste por última vez: parado no podrías barrer con ella, que
// es justo lo que uno hace con una linterna.
public class Flashlight : MonoBehaviour
{
    [SerializeField] PlayerController playerController;
    [SerializeField] PlayerCamera playerCamera;

    [Header("Luz")]
    [SerializeField] float range = 100f;

    [SerializeField] float spotAngle = 46f;
    [SerializeField] float innerAngle = 18f;

    // Muy alta a propósito, y no es un número al azar.
    //
    // La caída es cuadrática: a ocho unidades la luz llega 64 veces más débil que a una.
    // Sobre el suelo de PRL-000, que refleja el 18%, hace falta rondar los tres dígitos
    // para que a esa distancia se vea algo. Subir el alcance no sirve — el alcance solo
    // dice dónde se corta la luz, no cuánta llega.
    //
    // Cerca esto se pasa de 1 con holgura. De eso se encarga el tonemapping del perfil:
    // sin él se cortaría en blanco duro, y por eso hasta ahora había que dejarla floja.
    [SerializeField] float intensity = 120f;
    [SerializeField] Color color = new Color(1f, 0.93f, 0.82f);

    [Header("Montaje")]
    [Tooltip("Altura del foco de pie. Es la del casco, la misma que usa la vista en primera persona.")]
    [SerializeField] float standingHeight = 1.62f;
    [SerializeField] float crouchHeight = 1.02f;

    [Tooltip("Cuánto se adelanta el foco respecto del eje del personaje. Tiene que superar el radio del CharacterController (0,4) o la luz nace dentro del torso y lo único que ilumina bien es el propio cuerpo.")]
    [SerializeField] float muzzleOffset = 0.62f;

    [Tooltip("Inclinación hacia abajo cuando NO estás apuntando. Fija a propósito: una linterna que sigue el cabeceo de la cámara orbital apunta al suelo o al cielo según qué tan lejos esté la cámara, y se vuelve impredecible.")]
    [SerializeField] float restingPitch = 12f;

    [Header("Titileo")]
    [Tooltip("Cuánto varía la intensidad. Poco: es equipo gastado, no una antorcha.")]
    [SerializeField, Range(0f, 0.4f)] float flickerAmount = 0.07f;
    [SerializeField] float frameRate = 12f;

    Light spot;
    Transform mount;
    bool on;
    int lastFrame = -1;
    float flicker = 1f;

    void Awake()
    {
        mount = new GameObject("Lampara").transform;
        mount.SetParent(transform, false);

        spot = mount.gameObject.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.range = range;
        spot.spotAngle = spotAngle;
        spot.innerSpotAngle = innerAngle;
        spot.intensity = intensity;
        spot.color = color;
        spot.shadows = LightShadows.None;
        spot.enabled = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            on = !on;
            spot.enabled = on;
        }

        if (!on) return;

        // Titileo a saltos de 12 fps, igual que el fuego y el personaje. Interpolado se
        // vería de otro juego.
        int frame = Mathf.FloorToInt(Time.time * frameRate);
        if (frame != lastFrame)
        {
            lastFrame = frame;
            flicker = 1f - Random.value * flickerAmount;
        }

        spot.intensity = intensity * flicker;
    }

    // La colocación va en LateUpdate y no en Update: PlayerController mueve al personaje
    // en su propio Update, y el orden entre componentes no está garantizado. Acá el
    // jugador ya terminó de moverse este cuadro, así que la lámpara no arrastra.
    void LateUpdate()
    {
        if (playerCamera == null) return;

        // Primero la orientación. Apuntando sigue la mira exacta: si estás encarando
        // algo con la bengala, quieres verlo.
        bool aiming = playerController != null && playerController.Aiming;
        mount.rotation = aiming
            ? playerCamera.AimRotation
            : Quaternion.Euler(restingPitch, playerCamera.Yaw, 0f);

        // Y la posición SE DERIVA de esa orientación, no de la rotación del cuerpo.
        //
        // Acá estaba el error: el desplazamiento hacia adelante iba en coordenadas del
        // personaje, pero el personaje solo gira al caminar. Con el cuerpo mirando a un
        // lado y la cámara a otro, la luz salía de un costado del torso y apuntaba a otra
        // parte. Adelantándola sobre su propio eje, siempre sale por delante de la
        // dirección a la que ilumina.
        Vector3 plano = mount.forward;
        plano.y = 0f;
        if (plano.sqrMagnitude < 0.0001f) plano = transform.forward;
        plano.Normalize();

        bool crouching = playerController != null && playerController.IsCrouching;
        float altura = crouching ? crouchHeight : standingHeight;

        mount.position = transform.position + Vector3.up * altura + plano * muzzleOffset;
    }
}
