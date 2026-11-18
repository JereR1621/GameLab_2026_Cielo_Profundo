using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float distance = 6f;
    [SerializeField] float height = 2f;
    [SerializeField] float lookHeight = 1.2f;
    [SerializeField] float mouseSensitivity = 3f;

    [Header("El abismo debe seguir en cuadro: el rango de pitch se mantiene bajo")]
    [SerializeField] float minPitch = 5f;
    [SerializeField] float maxPitch = 45f;
    [SerializeField] float defaultPitch = 18f;

    float yaw;
    float pitch;

    public float Yaw => yaw;

    [Header("Primera persona (apuntado con la bengala)")]
    public bool FirstPerson;
    [SerializeField] Vector3 firstPersonOffset = new Vector3(0f, 1.62f, 0.12f);
    [Tooltip("Apuntando hace falta mirar mucho más arriba y abajo que caminando.")]
    [SerializeField] float aimMinPitch = -55f;
    [SerializeField] float aimMaxPitch = 70f;

    public Quaternion AimRotation => Quaternion.Euler(pitch, yaw, 0f);
    public Vector3 AimOrigin => target != null ? target.position + firstPersonOffset : transform.position;

    [Header("Cobertura: escondido en un cráter")]
    [SerializeField] float coverDistance = 2.6f;
    [SerializeField] float coverHeight = 2.5f;
    [SerializeField] float coverLookHeight = 1.7f;
    [Tooltip("Desplazamiento al hombro. Con la cámara centrada, la cabeza del personaje tapa justo lo que fuiste a mirar.")]
    [SerializeField] float coverShoulder = 0.7f;

    // Lo enciende PlayerHiding al meterse en el refugio.
    public bool CoverMode { get; set; }
    float coverBlend;

    [Header("Encuadre cinemático (lo maneja ItemPickupSequence)")]
    public bool InputLocked;
    public float CinematicBlend;
    public Vector3 CinematicPosition;
    public Quaternion CinematicRotation = Quaternion.identity;

    void Start()
    {
        pitch = defaultPitch;
        if (target != null) yaw = target.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Los ajustes del menú mandan sobre el valor del inspector. Se piden acá y no
        // solo desde el menú principal porque en el editor se entra a las escenas
        // directamente, sin pasar por él.
        GameSettings.EnsureApplied();
        mouseSensitivity = GameSettings.MouseSensitivity;
    }

    void Update()
    {
        // ESC ya no suelta el cursor: ahora abre la pausa, y de soltarlo se encarga ella.
        // Dejar los dos atajos hacía que en el mismo cuadro en que se abre la pausa esto
        // ya hubiera cambiado el estado del cursor, y al cerrarla se restauraba el estado
        // equivocado: te quedabas con el cursor suelto y la cámara muerta.
        if (InputLocked) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity * (GameSettings.InvertY ? -1f : 1f);
        pitch = FirstPerson
            ? Mathf.Clamp(pitch, aimMinPitch, aimMaxPitch)
            : Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (FirstPerson)
        {
            transform.position = AimOrigin;
            transform.rotation = AimRotation;
            return;
        }

        // Al salir del apuntado el pitch puede venir fuera del rango normal, así que
        // se lo trae de vuelta suave en vez de que la cámara pegue un salto.
        pitch = Mathf.MoveTowards(pitch, Mathf.Clamp(pitch, minPitch, maxPitch), Time.deltaTime * 120f);

        // Modo cobertura: metido en un cráter, la órbita normal deja la cámara enterrada
        // en el labio o mirando la cara interna de un bloque, y asomarse no serviría de
        // nada. Se acerca y se sube por encima del canto, desplazada al hombro.
        //
        // Es lo que hacen los sistemas de cobertura documentados: acercar y desplazar, sin
        // quitarle al jugador el control de hacia dónde mira. Fijar la cámara sería más
        // fácil y mucho peor — asomarse es justamente poder elegir qué mirar.
        coverBlend = Mathf.MoveTowards(coverBlend, CoverMode ? 1f : 0f, Time.deltaTime * 3.2f);

        float useDistance = Mathf.Lerp(distance, coverDistance, coverBlend);
        float useHeight = Mathf.Lerp(height, coverHeight, coverBlend);
        float useLookHeight = Mathf.Lerp(lookHeight, coverLookHeight, coverBlend);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 pivot = target.position + Vector3.up * useHeight
                      + rot * Vector3.right * (coverShoulder * coverBlend);

        Vector3 orbitPos = pivot - rot * Vector3.forward * useDistance;
        Quaternion orbitRot = Quaternion.LookRotation(
            target.position + Vector3.up * useLookHeight
            + rot * Vector3.right * (coverShoulder * coverBlend) - orbitPos);

        if (CinematicBlend > 0.001f)
        {
            // Se mezcla con la pose orbital en vez de reemplazarla, así entrar y salir
            // del encuadre cinemático no produce ningún salto de cámara.
            transform.position = Vector3.Lerp(orbitPos, CinematicPosition, CinematicBlend);
            transform.rotation = Quaternion.Slerp(orbitRot, CinematicRotation, CinematicBlend);
        }
        else
        {
            transform.position = orbitPos;
            transform.rotation = orbitRot;
        }
    }
}
