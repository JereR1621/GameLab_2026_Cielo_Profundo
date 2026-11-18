using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] Transform visualRoot;
    [SerializeField] PlayerController playerController;

    [Header("Cadencia")]
    [Tooltip("La pose se recalcula solo cuando cambia el cuadro. A 12 fps queda el escalonado de los juegos clásicos, no un movimiento interpolado.")]
    [SerializeField] float frameRate = 12f;

    [Header("Caminata")]
    [SerializeField] float walkCycleSpeed = 1.05f;
    [SerializeField] float speedForFullStride = 4f;
    [SerializeField] float thighSwing = 34f;
    [SerializeField] float kneeSwing = 62f;
    [SerializeField] float armSwing = 26f;
    [SerializeField] float elbowSwing = 34f;
    [SerializeField] float hipBob = 0.055f;
    [SerializeField] float hipRoll = 4.5f;
    [SerializeField] float spineTwist = 7f;
    [SerializeField] float walkLean = 7f;

    [Header("Reposo")]
    [Tooltip("Ni los brazos ni las piernas llegan nunca a estirarse del todo: un miembro perfectamente recto es justo lo que se lee como acartonado.")]
    [SerializeField] float restingElbowBend = 14f;
    [SerializeField] float restingKneeBend = 7f;
    [SerializeField] float breathAmount = 2.2f;

    [Header("Correr")]
    [Tooltip("Zancada más larga, brazos más altos y torso más volcado adelante.")]
    [SerializeField] float runSwingBoost = 1.45f;
    [SerializeField] float runLean = 17f;

    [Header("Agachado")]
    [SerializeField] float crouchDrop = 0.34f;
    [SerializeField] float crouchLean = 22f;
    [SerializeField] float crouchKnee = 68f;
    [SerializeField] float crouchThigh = -42f;
    [SerializeField] float crouchStrideScale = 0.5f;

    [Header("En el aire")]
    [SerializeField] float airThigh = -26f;
    [SerializeField] float airKnee = 48f;
    [SerializeField] float airShoulder = -48f;
    [SerializeField] float airElbow = -52f;

    [Header("Sostener el objeto en alto")]
    [SerializeField] float holdShoulder = -158f;
    [SerializeField] float holdElbow = -38f;
    [SerializeField] float holdLean = -8f;

    public bool HoldingUp { get; set; }
    public bool Struggling { get; set; }
    public bool Hiding { get; set; }

    // Altura del borde del cráter respecto de los pies. La pose de asomarse se deforma
    // con esto en vez de ser fija: cada bloque del labio mide distinto, y una pose
    // autorada para una altura concreta dejaría la cabeza tapada en los bloques altos y
    // el pecho afuera en los bajos.
    public float HideEdgeOffset { get; set; } = 1.4f;

    // 0 a 1 mientras se salta el labio. Fuera de la secuencia vale -1.
    public float VaultPhase { get; set; } = -1f;

    // ── Modo vitrina ────────────────────────────────────────────────────────────
    //
    // El maniquí de la pantalla de controles usa este mismo componente, pero no tiene
    // PlayerController ni un CharacterController que se mueva: está quieto en el aire,
    // lejísimos del terreno. Sin esto la pose se calcularía con velocidad cero y sin
    // suelo debajo, o sea que el maniquí se quedaría clavado en la pose de caída.
    //
    // Se sobrescriben las tres únicas cosas que la pose lee de fuera —velocidad, si
    // toca suelo, y si está agachado o corriendo— y el resto del animador es idéntico.
    // Esa es la gracia: lo que se ve en la pantalla de controles no es una imitación
    // de la animación del juego, es la animación del juego.
    public bool ShowcaseMode { get; set; }
    public float ShowcaseSpeed { get; set; }
    public bool ShowcaseCrouch { get; set; }
    public bool ShowcaseRun { get; set; }
    public bool ShowcaseAirborne { get; set; }

    CharacterController controller;

    Transform hips, spine, neck;
    Transform legL, legR, kneeL, kneeR, ankleL, ankleR;
    Transform shoulderL, shoulderR, elbowL, elbowR;

    Vector3 hipsRestPosition;
    float walkPhase;
    int lastFrame = -1;

    // ── Pisadas ─────────────────────────────────────────────────────────────────
    //
    // Avisa cada vez que un pie se apoya. El parámetro dice cuál: true es el derecho.
    //
    // Sale de acá y no de un temporizador aparte porque la fase de la caminata la lleva
    // este componente y avanza con la velocidad real. Enganchando el sonido a ella queda
    // clavado a la zancada que se está viendo, y al correr suenan más pisadas sin tener
    // que ajustar ningún ritmo a mano.
    //
    // Cuándo se apoya el pie sale del propio ciclo, no de probar valores: la rodilla se
    // levanta con Max(0, Sin(fase - PI/2)), que vuelve a cero en fase = 3PI/2 para la
    // pierna derecha. La izquierda va media vuelta corrida, así que le toca en PI/2. O
    // sea una pisada cada PI, en los múltiplos impares de PI/2.
    public event System.Action<bool> OnFootfall;

    int lastStepIndex = int.MinValue;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (visualRoot == null) visualRoot = transform.Find("Visual");
        if (visualRoot == null)
        {
            Debug.LogWarning("PlayerAnimator no encontró el objeto 'Visual'; se desactiva.");
            enabled = false;
            return;
        }

        hips = visualRoot.Find("Hips");
        if (hips == null)
        {
            Debug.LogWarning("PlayerAnimator no encontró el esqueleto; se desactiva.");
            enabled = false;
            return;
        }

        spine = hips.Find("Spine");
        neck = spine != null ? spine.Find("Neck") : null;

        legL = hips.Find("LegL");
        legR = hips.Find("LegR");
        kneeL = legL != null ? legL.Find("KneeL") : null;
        kneeR = legR != null ? legR.Find("KneeR") : null;
        ankleL = kneeL != null ? kneeL.Find("AnkleL") : null;
        ankleR = kneeR != null ? kneeR.Find("AnkleR") : null;

        shoulderL = spine != null ? spine.Find("ShoulderL") : null;
        shoulderR = spine != null ? spine.Find("ShoulderR") : null;
        elbowL = shoulderL != null ? shoulderL.Find("ElbowL") : null;
        elbowR = shoulderR != null ? shoulderR.Find("ElbowR") : null;

        if (spine == null || legL == null || legR == null || kneeL == null || kneeR == null ||
            shoulderL == null || shoulderR == null || elbowL == null || elbowR == null)
        {
            Debug.LogWarning("PlayerAnimator no encontró alguna articulación; se desactiva.");
            enabled = false;
            return;
        }

        hipsRestPosition = hips.localPosition;
    }

    void Update()
    {
        float horizontalSpeed;
        if (ShowcaseMode)
        {
            horizontalSpeed = ShowcaseSpeed;
        }
        else
        {
            Vector3 velocity = controller.velocity;
            horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        }

        // La fase avanza en tiempo real aunque la pose se muestre a 12 fps: así el
        // ciclo sigue atado a la velocidad real y no se desincroniza del movimiento.
        walkPhase += horizontalSpeed * walkCycleSpeed * Time.deltaTime;

        // Las pisadas se comprueban ANTES del recorte a 12 fps. La pose va escalonada a
        // propósito, pero el sonido no tiene por qué: cuantizarlo también le metería
        // hasta 80 ms de retraso contra el momento real del apoyo.
        ActualizarPisadas();

        int frame = Mathf.FloorToInt(Time.time * frameRate);
        if (frame == lastFrame) return;
        lastFrame = frame;

        ApplyPose(horizontalSpeed);
    }

    void ApplyPose(float horizontalSpeed)
    {
        if (Struggling) { ApplyStrugglePose(); return; }
        if (VaultPhase >= 0f) { ApplyVaultPose(VaultPhase); return; }
        if (Hiding) { ApplyHidePose(); return; }
        if (HoldingUp) { ApplyHoldPose(); return; }
        bool enElAire = ShowcaseMode ? ShowcaseAirborne : !controller.isGrounded;
        if (enElAire) { ApplyAirPose(); return; }

        bool crouching = ShowcaseMode
            ? ShowcaseCrouch
            : playerController != null && playerController.IsCrouching;
        bool running = ShowcaseMode
            ? ShowcaseRun
            : playerController != null && playerController.IsRunning;

        float stride = Mathf.Clamp01(horizontalSpeed / speedForFullStride);
        if (crouching) stride *= crouchStrideScale;

        // Correr no es solo ir más rápido: la zancada y el balanceo se agrandan,
        // si no se ve como caminar en cámara rápida.
        float swingScale = running ? runSwingBoost : 1f;

        float phaseR = walkPhase;
        float phaseL = walkPhase + Mathf.PI;

        // El muslo va y viene; la rodilla se dobla sobre todo cuando la pierna
        // viene hacia adelante, que es cuando tiene que despegar del suelo.
        float thighR = Mathf.Sin(phaseR) * thighSwing * stride * swingScale;
        float thighL = Mathf.Sin(phaseL) * thighSwing * stride * swingScale;
        float kneeAngleR = restingKneeBend + Mathf.Max(0f, Mathf.Sin(phaseR - Mathf.PI * 0.5f)) * kneeSwing * stride;
        float kneeAngleL = restingKneeBend + Mathf.Max(0f, Mathf.Sin(phaseL - Mathf.PI * 0.5f)) * kneeSwing * stride;

        // Los brazos contrabalancean a las piernas.
        float shoulderAngleR = -Mathf.Sin(phaseR) * armSwing * stride * swingScale;
        float shoulderAngleL = -Mathf.Sin(phaseL) * armSwing * stride * swingScale;
        float elbowAngleR = -(restingElbowBend + Mathf.Max(0f, Mathf.Sin(phaseR)) * elbowSwing * stride);
        float elbowAngleL = -(restingElbowBend + Mathf.Max(0f, Mathf.Sin(phaseL)) * elbowSwing * stride);

        float bob = Mathf.Abs(Mathf.Cos(phaseR)) * hipBob * stride;
        float roll = Mathf.Sin(phaseR) * hipRoll * stride;
        float twist = -Mathf.Sin(phaseR) * spineTwist * stride;
        float lean = walkLean * stride + (running ? runLean : 0f);

        if (crouching)
        {
            thighR += crouchThigh;
            thighL += crouchThigh;
            kneeAngleR += crouchKnee;
            kneeAngleL += crouchKnee;
            shoulderAngleR = Mathf.Lerp(shoulderAngleR, 22f, 0.6f);
            shoulderAngleL = Mathf.Lerp(shoulderAngleL, 22f, 0.6f);
            elbowAngleR = Mathf.Lerp(elbowAngleR, -55f, 0.6f);
            elbowAngleL = Mathf.Lerp(elbowAngleL, -55f, 0.6f);
            lean += crouchLean;
            bob -= crouchDrop;
        }
        else
        {
            // Respiración: pecho y hombros se mueven apenas cuando estás quieto.
            float breath = Mathf.Sin(Time.time * 1.6f) * breathAmount * (1f - stride);
            lean += breath * 0.35f;
            shoulderAngleR += breath * 0.5f;
            shoulderAngleL += breath * 0.5f;
            bob += Mathf.Sin(Time.time * 1.6f) * 0.012f * (1f - stride);
        }

        SetHips(bob, roll);
        spine.localRotation = Quaternion.Euler(lean, twist, -roll * 0.4f);
        if (neck != null) neck.localRotation = Quaternion.Euler(-lean * 0.55f, -twist * 0.6f, 0f);

        legR.localRotation = Quaternion.Euler(thighR, 0f, 0f);
        legL.localRotation = Quaternion.Euler(thighL, 0f, 0f);
        kneeR.localRotation = Quaternion.Euler(kneeAngleR, 0f, 0f);
        kneeL.localRotation = Quaternion.Euler(kneeAngleL, 0f, 0f);

        // El tobillo compensa muslo y rodilla para que el pie no apunte a cualquier lado.
        if (ankleR != null) ankleR.localRotation = Quaternion.Euler(-(thighR + kneeAngleR) * 0.55f, 0f, 0f);
        if (ankleL != null) ankleL.localRotation = Quaternion.Euler(-(thighL + kneeAngleL) * 0.55f, 0f, 0f);

        shoulderR.localRotation = Quaternion.Euler(shoulderAngleR, 0f, 0f);
        shoulderL.localRotation = Quaternion.Euler(shoulderAngleL, 0f, 0f);
        elbowR.localRotation = Quaternion.Euler(elbowAngleR, 0f, 0f);
        elbowL.localRotation = Quaternion.Euler(elbowAngleL, 0f, 0f);
    }

    void ActualizarPisadas()
    {
        if (ShowcaseMode || OnFootfall == null) return;

        int indice = Mathf.FloorToInt((walkPhase - Mathf.PI * 0.5f) / Mathf.PI);

        // La primera vuelta solo toma nota. Sin esto sonaría una pisada al aparecer en el
        // planeta, de pie y sin haberse movido.
        if (lastStepIndex == int.MinValue) { lastStepIndex = indice; return; }
        if (indice == lastStepIndex) return;

        lastStepIndex = indice;

        // El índice se actualiza igual aunque no vaya a sonar. Si se saliera antes, al
        // caer al suelo o al salir del escondite sonaría de golpe la pisada que quedó
        // pendiente de hace rato.
        if (!controller.isGrounded) return;
        if (Struggling || Hiding || VaultPhase >= 0f) return;

        // Par es izquierdo: en fase PI/2 el índice es 0 y le toca a esa pierna.
        OnFootfall(((indice % 2) + 2) % 2 == 1);
    }

    void ApplyAirPose()
    {
        SetHips(0f, 0f);
        spine.localRotation = Quaternion.Euler(6f, 0f, 0f);
        if (neck != null) neck.localRotation = Quaternion.identity;

        // Una pierna más recogida que la otra: simétrico se ve como un maniquí.
        legR.localRotation = Quaternion.Euler(airThigh, 0f, 0f);
        legL.localRotation = Quaternion.Euler(airThigh * 0.45f, 0f, 0f);
        kneeR.localRotation = Quaternion.Euler(airKnee, 0f, 0f);
        kneeL.localRotation = Quaternion.Euler(airKnee * 0.5f, 0f, 0f);
        if (ankleR != null) ankleR.localRotation = Quaternion.Euler(-18f, 0f, 0f);
        if (ankleL != null) ankleL.localRotation = Quaternion.Euler(-10f, 0f, 0f);

        shoulderR.localRotation = Quaternion.Euler(airShoulder, 0f, -14f);
        shoulderL.localRotation = Quaternion.Euler(airShoulder, 0f, 14f);
        elbowR.localRotation = Quaternion.Euler(airElbow, 0f, 0f);
        elbowL.localRotation = Quaternion.Euler(airElbow, 0f, 0f);
    }

    // Forcejeo: manotazos rápidos y desordenados. Se anima a más frecuencia que el
    // resto a propósito, porque el pánico tiene que verse distinto de caminar.
    void ApplyStrugglePose()
    {
        float f = Time.time * 11f;
        float flailR = Mathf.Sin(f) * 42f;
        float flailL = Mathf.Sin(f + 2.1f) * 42f;

        SetHips(-0.18f + Mathf.Sin(f * 0.8f) * 0.03f, Mathf.Sin(f * 0.6f) * 6f);
        spine.localRotation = Quaternion.Euler(-16f + Mathf.Sin(f * 0.7f) * 5f, Mathf.Sin(f * 0.5f) * 9f, 0f);
        if (neck != null) neck.localRotation = Quaternion.Euler(-22f, Mathf.Sin(f * 0.9f) * 12f, 0f);

        legR.localRotation = Quaternion.Euler(-34f + flailR * 0.5f, 0f, 0f);
        legL.localRotation = Quaternion.Euler(-34f + flailL * 0.5f, 0f, 0f);
        kneeR.localRotation = Quaternion.Euler(52f + Mathf.Abs(flailR) * 0.4f, 0f, 0f);
        kneeL.localRotation = Quaternion.Euler(52f + Mathf.Abs(flailL) * 0.4f, 0f, 0f);
        if (ankleR != null) ankleR.localRotation = Quaternion.identity;
        if (ankleL != null) ankleL.localRotation = Quaternion.identity;

        shoulderR.localRotation = Quaternion.Euler(-118f + flailR, 0f, -18f);
        shoulderL.localRotation = Quaternion.Euler(-118f + flailL, 0f, 18f);
        elbowR.localRotation = Quaternion.Euler(-46f - Mathf.Abs(flailR) * 0.4f, 0f, 0f);
        elbowL.localRotation = Quaternion.Euler(-46f - Mathf.Abs(flailL) * 0.4f, 0f, 0f);
    }

    // Asomado sobre el borde del cráter: manos apoyadas en el canto y la cabeza justo por
    // encima. Es lo que se ve desde afuera.
    //
    // La pose es fija y lo que se deforma es la CADERA, según lo alto que sea el bloque
    // concreto contra el que te apoyas. Es el mismo principio que usan los mantles
    // parametrizados: autorar la postura una vez y estirar la raíz al obstáculo real, en
    // vez de tener una animación por altura. Sin esto, con el labio variando entre 1,1 y
    // 1,8 la cabeza quedaría tapada en los bloques altos y el pecho afuera en los bajos.
    //
    // Y que se te vea la cabeza no te delata: la araña es ciega. El ruido sale del cuerpo,
    // que queda debajo del canto. Asomarse es información para vos, no un riesgo.
    void ApplyHidePose()
    {
        float breath = Mathf.Sin(Time.time * 1.1f);

        // Altura de cadera para que los hombros queden a la altura del canto. El borde se
        // mide desde los pies; la cadera de pie está a hipsRestPosition.y, así que la
        // corrección es la diferencia, acotada para que nunca quede de puntillas ni
        // sentada en el suelo.
        float shoulderFromHips = 0.62f;
        float wanted = HideEdgeOffset - shoulderFromHips;
        float lift = Mathf.Clamp(wanted - hipsRestPosition.y, -0.62f, 0.05f);

        SetHips(lift + breath * 0.006f, 0f);

        // Inclinado hacia el borde, no encogido: el pecho contra la pared interna.
        spine.localRotation = Quaternion.Euler(24f, 0f, 0f);
        if (neck != null) neck.localRotation = Quaternion.Euler(-22f, 14f, 0f);

        // Piernas dobladas lo que haga falta según cuánto bajó la cadera. Con el labio
        // bajo queda casi de pie; con el labio alto, en cuclillas.
        float squat = Mathf.InverseLerp(0.05f, -0.62f, lift);
        float thigh = Mathf.Lerp(-14f, -74f, squat);
        float knee = Mathf.Lerp(restingKneeBend, 92f, squat);

        legR.localRotation = Quaternion.Euler(thigh, 0f, -6f);
        legL.localRotation = Quaternion.Euler(thigh, 0f, 6f);
        kneeR.localRotation = Quaternion.Euler(knee, 0f, 0f);
        kneeL.localRotation = Quaternion.Euler(knee, 0f, 0f);
        if (ankleR != null) ankleR.localRotation = Quaternion.Euler(-thigh * 0.35f, 0f, 0f);
        if (ankleL != null) ankleL.localRotation = Quaternion.Euler(-thigh * 0.35f, 0f, 0f);

        // Brazos al frente y arriba: las manos apoyadas en el canto.
        shoulderR.localRotation = Quaternion.Euler(-64f + breath * 1.1f, 0f, -12f);
        shoulderL.localRotation = Quaternion.Euler(-64f + breath * 1.1f, 0f, 12f);
        elbowR.localRotation = Quaternion.Euler(38f, 0f, 0f);
        elbowL.localRotation = Quaternion.Euler(38f, 0f, 0f);
    }

    // El salto por encima del labio, en tres tiempos. La raíz la mueve PlayerHiding por
    // una curva calculada al bloque real; acá solo van las articulaciones.
    //
    // Corta a propósito: los saltos de cobertura que se sienten bien duran medio segundo.
    // Uno lento se convierte en un peaje que ves decenas de veces por partida.
    void ApplyVaultPose(float t)
    {
        // 0 a 0,35 plantar · 0,35 a 0,7 pasar la pierna · 0,7 a 1 caer
        if (t < 0.35f)
        {
            float k = t / 0.35f;

            // Manos al canto y cuerpo echado adelante.
            SetHips(Mathf.Lerp(0f, -0.12f, k), 0f);
            spine.localRotation = Quaternion.Euler(Mathf.Lerp(6f, 42f, k), 0f, 0f);
            if (neck != null) neck.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -26f, k), 0f, 0f);

            shoulderR.localRotation = Quaternion.Euler(Mathf.Lerp(-10f, -96f, k), 0f, -10f);
            shoulderL.localRotation = Quaternion.Euler(Mathf.Lerp(-10f, -96f, k), 0f, 10f);
            elbowR.localRotation = Quaternion.Euler(Mathf.Lerp(restingElbowBend, 14f, k), 0f, 0f);
            elbowL.localRotation = Quaternion.Euler(Mathf.Lerp(restingElbowBend, 14f, k), 0f, 0f);

            legR.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -28f, k), 0f, -4f);
            legL.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 12f, k), 0f, 4f);
            kneeR.localRotation = Quaternion.Euler(Mathf.Lerp(restingKneeBend, 52f, k), 0f, 0f);
            kneeL.localRotation = Quaternion.Euler(restingKneeBend, 0f, 0f);
        }
        else if (t < 0.7f)
        {
            float k = (t - 0.35f) / 0.35f;

            // La pierna derecha cruza por encima. La cadera rueda para acompañarla, que es
            // lo que hace que se lea como pasar por arriba y no como flotar.
            SetHips(-0.12f, Mathf.Lerp(0f, -16f, k));
            spine.localRotation = Quaternion.Euler(42f, Mathf.Lerp(0f, 18f, k), 0f);

            shoulderR.localRotation = Quaternion.Euler(-96f, 0f, -10f);
            shoulderL.localRotation = Quaternion.Euler(-96f, 0f, 10f);
            elbowR.localRotation = Quaternion.Euler(14f, 0f, 0f);
            elbowL.localRotation = Quaternion.Euler(14f, 0f, 0f);

            legR.localRotation = Quaternion.Euler(Mathf.Lerp(-28f, -96f, k), 0f, Mathf.Lerp(-4f, -34f, k));
            kneeR.localRotation = Quaternion.Euler(Mathf.Lerp(52f, 88f, k), 0f, 0f);
            legL.localRotation = Quaternion.Euler(Mathf.Lerp(12f, -34f, k), 0f, 4f);
            kneeL.localRotation = Quaternion.Euler(Mathf.Lerp(restingKneeBend, 46f, k), 0f, 0f);
        }
        else
        {
            float k = (t - 0.7f) / 0.3f;

            // Caída: las rodillas absorben y los brazos sueltan el canto.
            SetHips(Mathf.Lerp(-0.12f, -0.4f, k), Mathf.Lerp(-16f, 0f, k));
            spine.localRotation = Quaternion.Euler(Mathf.Lerp(42f, 26f, k), Mathf.Lerp(18f, 0f, k), 0f);
            if (neck != null) neck.localRotation = Quaternion.Euler(Mathf.Lerp(-26f, -18f, k), 0f, 0f);

            shoulderR.localRotation = Quaternion.Euler(Mathf.Lerp(-96f, -64f, k), 0f, -12f);
            shoulderL.localRotation = Quaternion.Euler(Mathf.Lerp(-96f, -64f, k), 0f, 12f);
            elbowR.localRotation = Quaternion.Euler(Mathf.Lerp(14f, 38f, k), 0f, 0f);
            elbowL.localRotation = Quaternion.Euler(Mathf.Lerp(14f, 38f, k), 0f, 0f);

            float thigh = Mathf.Lerp(-96f, -52f, k);
            legR.localRotation = Quaternion.Euler(thigh, 0f, Mathf.Lerp(-34f, -6f, k));
            legL.localRotation = Quaternion.Euler(Mathf.Lerp(-34f, -52f, k), 0f, 6f);
            kneeR.localRotation = Quaternion.Euler(Mathf.Lerp(88f, 74f, k), 0f, 0f);
            kneeL.localRotation = Quaternion.Euler(Mathf.Lerp(46f, 74f, k), 0f, 0f);
        }

        if (ankleR != null) ankleR.localRotation = Quaternion.identity;
        if (ankleL != null) ankleL.localRotation = Quaternion.identity;
    }

    void ApplyHoldPose()
    {
        float sway = Mathf.Sin(Time.time * 1.4f) * 1.6f;

        SetHips(Mathf.Sin(Time.time * 1.4f) * 0.01f, 0f);
        spine.localRotation = Quaternion.Euler(holdLean, 0f, 0f);
        if (neck != null) neck.localRotation = Quaternion.Euler(12f, 0f, 0f);

        legR.localRotation = Quaternion.Euler(-4f, 0f, 0f);
        legL.localRotation = Quaternion.Euler(4f, 0f, 0f);
        kneeR.localRotation = Quaternion.Euler(restingKneeBend + 6f, 0f, 0f);
        kneeL.localRotation = Quaternion.Euler(restingKneeBend + 6f, 0f, 0f);
        if (ankleR != null) ankleR.localRotation = Quaternion.identity;
        if (ankleL != null) ankleL.localRotation = Quaternion.identity;

        shoulderR.localRotation = Quaternion.Euler(holdShoulder + sway, 0f, -10f);
        shoulderL.localRotation = Quaternion.Euler(holdShoulder + sway, 0f, 10f);
        elbowR.localRotation = Quaternion.Euler(holdElbow, 0f, 0f);
        elbowL.localRotation = Quaternion.Euler(holdElbow, 0f, 0f);
    }

    void SetHips(float verticalOffset, float roll)
    {
        hips.localPosition = hipsRestPosition + new Vector3(0f, verticalOffset, 0f);
        hips.localRotation = Quaternion.Euler(0f, 0f, roll);
    }
}
