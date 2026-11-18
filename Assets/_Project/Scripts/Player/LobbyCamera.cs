using UnityEngine;

// Cámara completamente fija: no sigue al jugador ni rota nunca. El cuarto entero
// entra en cuadro, así que moverse dentro no necesita que la cámara acompañe.
//
// Como el yaw queda constante en 45 grados, PlayerController sigue funcionando sin
// cambios: mueve al jugador relativo al yaw de la cámara, que es exactamente el
// control diagonal que corresponde a una vista isométrica.
public class LobbyCamera : MonoBehaviour
{
    [SerializeField] Vector3 position = new Vector3(23f, 23f, -23f);
    [SerializeField] Vector3 lookAt = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Campo de visión angosto: aplana la perspectiva y acerca el resultado a una isométrica.")]
    [SerializeField] float fieldOfView = 38f;

    void Awake()
    {
        Apply();
    }

    // La nave reemplaza a PlayerCamera por esta, y PlayerCamera era la única que fijaba
    // el estado del cursor. Sin esto, al llegar a la nave el cursor quedaba como lo
    // hubiera dejado la escena anterior, distinto según vinieras del menú, del planeta
    // o de una muerte.
    void Start()
    {
        GameSettings.EnsureApplied();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void LateUpdate()
    {
        // Se reafirma cada cuadro por si algo más intentó mover la cámara.
        Apply();
    }

    void Apply()
    {
        transform.position = position;
        transform.rotation = Quaternion.LookRotation((lookAt - position).normalized, Vector3.up);

        var cam = GetComponent<Camera>();
        if (cam != null) cam.fieldOfView = fieldOfView;
    }
}
