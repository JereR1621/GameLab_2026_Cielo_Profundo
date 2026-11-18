using UnityEngine;

// Meterse en un refugio. Estado del jugador; el puesto concreto lo ofrece HideSpot.
//
// Escondido dejas de hacer prácticamente ruido, que es lo único que la araña percibe.
// No es invulnerabilidad: si ya te venía siguiendo y llega hasta el cráter, te agarra
// igual. Es una forma de romperle el rastro, no de desaparecer.
public class PlayerHiding : MonoBehaviour
{
    public static PlayerHiding Instance { get; private set; }

    [SerializeField] PlayerController playerController;
    [SerializeField] PlayerAnimator playerAnimator;
    [SerializeField] InteractionPromptUI prompt;
    [SerializeField] CharacterController controller;
    [SerializeField] PlayerCamera playerCamera;

    [Tooltip("Cuánto dura el salto por encima del labio. Medio segundo: los saltos de cobertura que se sienten bien son cortos, y uno lento se vuelve un peaje.")]
    [SerializeField] float vaultDuration = 0.55f;

    [Tooltip("Cuánto pasa el cuerpo por encima del canto en el punto más alto del arco.")]
    [SerializeField] float apexClearance = 0.45f;

    public bool IsHiding { get; private set; }
    public InteractionPromptUI Prompt => prompt;

    HideSpot spot;
    Vector3 fromPosition;
    Quaternion fromRotation;
    Vector3 toPosition;
    Quaternion toRotation;
    Vector3 vaultFrom, vaultTo, vaultApex;
    float vault;
    bool vaulting;
    bool vaultIn;

    // Frame del último cambio de estado, en las DOS direcciones.
    //
    // Entrando: la misma E que te mete llegaría al Update de acá y te sacaría en el acto.
    // Saliendo: es peor, porque PlayerHiding y HideSpot corren en el mismo cuadro y el
    // orden entre componentes no está garantizado. Si este sale primero, HideSpot ve
    // IsHiding en falso con la E todavía pulsada y te vuelve a meter — que es justo el
    // bug de no poder salir. Es el mismo resguardo simétrico que llevan los paneles.
    int lastToggleFrame = -1;

    public bool ToggledThisFrame => Time.frameCount == lastToggleFrame;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Enter(HideSpot target)
    {
        if (IsHiding || target == null) return;

        IsHiding = true;
        spot = target;

        fromPosition = transform.position;
        fromRotation = transform.rotation;
        toPosition = target.StandPosition;

        toRotation = target.KeepFacing
            ? transform.rotation
            : Quaternion.LookRotation(target.FacingDirection, Vector3.up);

        lastToggleFrame = Time.frameCount;

        // El controlador se apaga para que no pelee con la colocación. Mientras dura el
        // salto, el movimiento lo maneja esto.
        if (playerController != null) playerController.enabled = false;
        if (controller != null) controller.enabled = false;

        if (playerAnimator != null)
        {
            playerAnimator.Hiding = true;
            playerAnimator.HideEdgeOffset = target.EdgeOffset;
        }

        if (playerCamera != null) playerCamera.CoverMode = true;

        UISound.Click();
        StartVault(true);
    }

    // ── El salto ────────────────────────────────────────────────────────────────
    //
    // La animación no es fija: se DEFORMA al bloque real. Es el principio de los mantles
    // parametrizados —el mismo que usan los saltos de cobertura de los juegos que los
    // hacen bien— y acá es obligatorio, porque cada bloque del labio mide distinto y una
    // curva autorada para una altura concreta dejaría los pies atravesando la piedra en
    // la mitad de los cráteres.
    //
    // El cuerpo viaja por una Bézier cuadrática cuyo vértice se calcula con el canto
    // medido, y las articulaciones van encima en tres tiempos. Medio segundo: los saltos
    // de cobertura que se sienten bien son cortos, y uno lento se vuelve un peaje que ves
    // decenas de veces por partida.

    void StartVault(bool entering)
    {
        vaulting = true;
        vaultIn = entering;
        vault = 0f;

        Vector3 a = entering ? fromPosition : toPosition;
        Vector3 b = entering ? toPosition : fromPosition;

        float edge = spot != null ? spot.EdgeOffset : 1.4f;

        // Vértice por encima del canto, no a media altura entre los extremos: es lo que
        // garantiza que el cuerpo pase POR ARRIBA de la piedra y no la atraviese.
        vaultApex = (a + b) * 0.5f;
        vaultApex.y = Mathf.Max(a.y, b.y) + edge + apexClearance;

        vaultFrom = a;
        vaultTo = b;
    }

    Vector3 VaultPoint(float t)
    {
        // Bézier cuadrática: una interpolación recta entre extremo y vértice haría un
        // pico anguloso, y esto da el arco continuo del salto.
        float u = 1f - t;
        return u * u * vaultFrom + 2f * u * t * vaultApex + t * t * vaultTo;
    }

    public void Exit()
    {
        if (!IsHiding || vaulting) return;

        lastToggleFrame = Time.frameCount;

        // Sale saltando también, por el mismo lado por el que entró. Salir apurado del
        // agujero es el momento con más tensión de toda la mecánica.
        UISound.Click();
        StartVault(false);
    }

    // Salida inmediata, sin salto. La usa la captura de la araña: si te saca del pozo no
    // hay animación que valga, y dejar las dos secuencias vivas a la vez las pone a
    // pelearse por la pose, por la cámara y por la tecla E.
    //
    // Devuelve al jugador a donde estaba ANTES de meterse. Además de ser lo que cuenta la
    // escena —te arranca de ahí—, es lo que evita dejarlo encerrado: el brocal es un
    // anillo cerrado y sin la salida saltada se quedaría dentro sin forma de salir.
    public void ForceRelease()
    {
        if (!IsHiding) return;

        vaulting = false;
        transform.position = fromPosition;
        FinishExit();
    }

    // Se llama al terminar el salto de salida, no al pulsar E: mientras dura, el jugador
    // todavía está en el aire y devolverle el control lo dejaría cayendo a medio arco.
    void FinishExit()
    {
        IsHiding = false;
        spot = null;

        if (controller != null) controller.enabled = true;
        if (playerController != null) playerController.enabled = true;

        if (playerAnimator != null)
        {
            playerAnimator.Hiding = false;
            playerAnimator.VaultPhase = -1f;
        }

        if (playerCamera != null) playerCamera.CoverMode = false;
    }

    void Update()
    {
        if (!IsHiding) return;

        if (vaulting)
        {
            vault = Mathf.Clamp01(vault + Time.deltaTime / Mathf.Max(0.05f, vaultDuration));

            transform.position = VaultPoint(vault);
            transform.rotation = Quaternion.Slerp(fromRotation, toRotation,
                                                  vaultIn ? vault : 1f - vault);

            if (playerAnimator != null)
                playerAnimator.VaultPhase = vaultIn ? vault : 1f - vault;

            if (vault < 1f) return;

            vaulting = false;
            if (playerAnimator != null) playerAnimator.VaultPhase = -1f;

            if (!vaultIn) { FinishExit(); return; }
        }

        prompt?.Request("salir del escondite", 0f);

        if (!ToggledThisFrame && Input.GetKeyDown(KeyCode.E)) Exit();
    }
}
