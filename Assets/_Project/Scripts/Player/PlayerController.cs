using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float turnSmoothTime = 0.08f;

    [Header("Salto")]
    [SerializeField] float jumpForce = 9f;
    [SerializeField] float gravity = -18f;
    [Tooltip("Se apaga en la nave: sin salto y con el escalón bajo, no hay forma de treparse al mobiliario y quedar en lugares raros.")]
    [SerializeField] bool canJump = true;

    [Header("Planeo")]
    [SerializeField] float glideGravity = -2.5f;
    [SerializeField] float glideMaxFallSpeed = -2f;

    [Header("Sigilo")]
    [SerializeField] float crouchSpeed = 2.2f;
    [SerializeField] float standingHeight = 2f;
    [SerializeField] float crouchHeight = 1.15f;

    [Header("Correr")]
    [SerializeField] float runSpeed = 10.5f;
    [SerializeField] float maxStamina = 100f;

    [Tooltip("Cuánto multiplica el aguante la ración de oxígeno comprada en la nave. Vale para un descenso.")]
    [SerializeField] float staminaPrepBonus = 1.45f;
    [Tooltip("Corto a propósito: correr tiene que ser un recurso, no la forma normal de moverse.")]
    [SerializeField] float staminaDrainPerSecond = 34f;
    [SerializeField] float staminaRegenPerSecond = 19f;
    [Tooltip("Al agotarse no puedes volver a correr hasta recuperar esto. Evita el shift a repetición.")]
    [SerializeField] float staminaToRecover = 34f;

    [Header("Referencias")]
    [SerializeField] Transform cameraPivot;

    CharacterController controller;
    Vector3 velocity;
    float turnSmoothVelocity;

    public bool IsGrounded { get; private set; }
    public bool IsGliding { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsRunning { get; private set; }

    [Header("Apuntado")]
    [Tooltip("Apuntando caminas muy lento, pero no te clavas: frenar del todo se siente un freno de mano.")]
    [SerializeField] float aimSpeed = 1.6f;
    public bool Aiming { get; set; }
    public float MaxPlanarSpeed => moveSpeed;
    public float RunSpeed => runSpeed;

    float stamina;
    bool exhausted;

    public float Stamina01 => maxStamina > 0f ? stamina / maxStamina : 0f;
    public bool IsExhausted => exhausted;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        // Ración de oxígeno: comprada en la nave, vale para ESTE descenso. Sube el techo
        // de aguante, no la velocidad ni el consumo — te deja correr tramos más largos
        // sin volverte más rápido ni más silencioso, así que no rompe nada.
        if (Expedition.StaminaPrepReady) maxStamina *= staminaPrepBonus;

        stamina = maxStamina;
    }

    void Update()
    {
        IsGrounded = controller.isGrounded;
        UpdateCrouch();
        UpdateStamina();
        Vector3 planarMotion = ComputePlanarMotion();
        UpdateVerticalVelocity();

        // Un solo Move por frame, combinando horizontal y vertical. Si se llama dos
        // veces, controller.velocity queda con los datos de la última llamada nada más
        // (y el animador leería velocidad horizontal cero mientras caminas).
        Vector3 motion = planarMotion + Vector3.up * velocity.y;
        controller.Move(motion * Time.deltaTime);
    }

    // Agacharse: Ctrl mantenido. Se baja la altura y el centro juntos, para que los
    // pies queden donde estaban en vez de que el jugador se hunda o salte.
    void UpdateCrouch()
    {
        IsCrouching = IsGrounded && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));

        float targetHeight = IsCrouching ? crouchHeight : standingHeight;
        controller.height = Mathf.MoveTowards(controller.height, targetHeight, Time.deltaTime * 6f);
        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
    }

    void UpdateStamina()
    {
        bool wantsToRun = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool moving = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).sqrMagnitude > 0.01f;

        IsRunning = wantsToRun && moving && IsGrounded && !IsCrouching && !Aiming && !exhausted && stamina > 0f;

        if (IsRunning)
        {
            stamina -= staminaDrainPerSecond * Time.deltaTime;
            if (stamina <= 0f)
            {
                stamina = 0f;
                exhausted = true;
            }
        }
        else
        {
            stamina = Mathf.Min(maxStamina, stamina + staminaRegenPerSecond * Time.deltaTime);
            if (exhausted && stamina >= staminaToRecover) exhausted = false;
        }
    }

    Vector3 ComputePlanarMotion()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(h, 0f, v).normalized;
        if (cameraPivot == null) return Vector3.zero;

        // Apuntando el cuerpo mira hacia donde mira la cámara, no hacia donde caminas.
        if (Aiming)
        {
            transform.rotation = Quaternion.Euler(0f, cameraPivot.eulerAngles.y, 0f);
            if (inputDir.magnitude < 0.1f) return Vector3.zero;

            Vector3 aimMove = Quaternion.Euler(0f, cameraPivot.eulerAngles.y, 0f) * inputDir;
            return aimMove.normalized * aimSpeed;
        }

        if (inputDir.magnitude < 0.1f) return Vector3.zero;

        float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cameraPivot.eulerAngles.y;
        float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
        transform.rotation = Quaternion.Euler(0f, angle, 0f);

        Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        float speed = IsCrouching ? crouchSpeed : (IsRunning ? runSpeed : moveSpeed);
        return moveDir.normalized * speed;
    }

    void UpdateVerticalVelocity()
    {
        if (IsGrounded && velocity.y < 0f)
            velocity.y = -2f;

        if (canJump && IsGrounded && Input.GetButtonDown("Jump"))
            velocity.y = jumpForce;

        IsGliding = canJump && !IsGrounded && Input.GetButton("Jump") && velocity.y < 0f;

        float currentGravity = IsGliding ? glideGravity : gravity;
        velocity.y += currentGravity * Time.deltaTime;
        if (IsGliding && velocity.y < glideMaxFallSpeed)
            velocity.y = glideMaxFallSpeed;
    }
}
