using System.Collections.Generic;
using UnityEngine;

// Araña de patas largas con locomoción procedural. No hay ciclo de caminata grabado:
// cada pata mira cuánto se estiró respecto de donde debería estar y decide sola
// cuándo dar el paso. Ese desfase irregular entre las ocho es lo que la hace ver viva.
public class SpiderCreature : MonoBehaviour
{
    [Header("Cuerpo")]
    [SerializeField] Material bodyMaterial;
    [SerializeField] float bodyHeight = 1.75f;
    [SerializeField] float bodyLength = 1.1f;

    [Header("Patas")]
    [SerializeField] int legsPerSide = 4;
    [SerializeField] float femurLength = 1.5f;
    [SerializeField] float tibiaLength = 1.9f;
    [SerializeField] float legReach = 2.4f;
    [SerializeField] float legSpreadZ = 2.2f;
    [SerializeField] float legThickness = 0.09f;

    [Header("Paso")]
    [Tooltip("Cuánto puede alejarse un pie de su sitio ideal antes de verse obligado a dar el paso.")]
    [SerializeField] float stepThreshold = 1.15f;
    [SerializeField] float stepDuration = 0.26f;
    [SerializeField] float stepHeight = 0.6f;
    [Tooltip("Cuántas patas pueden estar en el aire a la vez. Bajo = anda firme, alto = se ve nerviosa.")]
    [SerializeField] int maxSimultaneousSteps = 3;

    [Header("Deambular errático")]
    [Tooltip("Se mueve a ráfagas cortas de duración y velocidad al azar, no a paso constante.")]
    [SerializeField] float moveSpeed = 1.9f;
    [SerializeField] float turnSpeed = 130f;
    [SerializeField] float wanderRadius = 26f;
    [SerializeField] Vector2 burstDuration = new Vector2(0.25f, 1.3f);
    [SerializeField] Vector2 pauseDuration = new Vector2(0.3f, 2.6f);
    [Tooltip("Cuánto puede desviarse de golpe al arrancar una ráfaga.")]
    [SerializeField] float burstTurnJitter = 75f;

    [Header("Tempo: cambia de golpe, no acelera")]
    [Tooltip("Un bicho no acelera: está quieto o está a velocidad. Por eso el cambio es instantáneo y no interpolado.")]
    [SerializeField] Vector2 tempoInterval = new Vector2(0.14f, 0.85f);
    [SerializeField] Vector2 tempoSpeedRange = new Vector2(0.15f, 2.6f);
    [Tooltip("Cada tanto pega un tirón brevísimo a velocidad muy alta.")]
    [SerializeField, Range(0f, 1f)] float twitchChance = 0.14f;
    [SerializeField] float twitchSpeed = 4.6f;
    [SerializeField] Vector2 twitchDuration = new Vector2(0.07f, 0.22f);
    [Tooltip("Cada tanto reacomoda una pata sin moverse del lugar, como hacen las arañas.")]
    [SerializeField] Vector2 fidgetInterval = new Vector2(0.5f, 2.8f);

    [Header("Caza por sonido")]
    [SerializeField] float huntSpeed = 3.4f;

    // La bengala lo necesita para calcular cuánto tarda cada araña en llegar hasta
    // ella, y darle ese tiempo antes de mandarla a dispersarse.
    public float HuntSpeed => huntSpeed;
    [Tooltip("Cuánto sigue yendo hacia el último ruido después de dejar de oírte.")]
    [SerializeField] float alertMemory = 6f;
    [Tooltip("Se queda tiesa un instante al oírte por primera vez, antes de arrancar.")]
    [SerializeField] float startleDuration = 0.7f;

    [Header("Oclusión: lo que tapa el sonido")]
    [Tooltip("Qué fracción del radio de audición queda cuando hay un cráter de por medio. 0.3 = te oye a menos de un tercio de distancia.")]
    [SerializeField, Range(0.05f, 1f)] float occludedHearing = 0.3f;

    [Tooltip("Altura de la cabeza de la araña. Es desde donde se traza la línea hacia el jugador.")]
    [SerializeField] float earHeight = 1.5f;

    // Multiplicador del oído, que pone el generador según el tramo de la ruta.
    //
    // El radio de audición es del JUGADOR, no de la araña: lo emite NoiseEmitter según
    // lo que estés haciendo. Eso está bien —el ruido lo haces tú— pero deja sin ninguna
    // palanca para que un bicho oiga mejor que otro. Esto es esa palanca, y es lo que
    // permite que las arañas del último tramo sean de verdad peores y no solo más.
    public float HearingScale { get; set; } = 1f;

    // Multiplicador de la velocidad de caza, por el mismo motivo que el del oído: dar
    // una palanca para que la presión suba sin que suba el número de bichos.
    public float HuntScale { get; set; } = 1f;

    // La manda a un punto concreto como si lo hubiera oído. La usa el cierre del
    // catálogo: cuando levantas la última pieza, todas saben por dónde ibas.
    public void Alert(Vector3 position)
    {
        if (IsStunned || IsCapturing || FrozenByCinematic) return;
        lastHeardPosition = position;
        alertTimer = alertMemory;
    }

    [Header("Separación entre arañas")]
    [Tooltip("A qué distancia empiezan a empujarse entre sí. Es lo que evita que se encimen al converger sobre una bengala.")]
    [SerializeField] float separationRadius = 3.6f;
    [SerializeField] float separationStrength = 4f;

    [Header("Captura")]
    [SerializeField] float catchRadius = 2.3f;

    [Tooltip("Cuánto se queda por fuera del brocal. Es solo cosa de que no se meta dentro de la piedra: dentro del pozo no puede atrapar a nadie.")]
    [SerializeField] float wellClearance = 0.9f;
    [Tooltip("Cuánto queda tiesa después de que te zafas, para darte tiempo de huir.")]
    [SerializeField] float stunDuration = 7f;
    [Tooltip("Cuánto se alza el cuerpo al encabritarse sobre el jugador.")]
    [SerializeField] float threatBodyLift = 1.3f;

    [Header("Cadencia")]
    [Tooltip("Misma cadencia que el personaje y el fuego: si se moviera suave, se vería de otro juego.")]
    [SerializeField] float frameRate = 12f;

    class Leg
    {
        public Vector3 hipLocal;
        public Vector3 restLocal;
        public Transform femur;
        public Transform tibia;
        public Vector3 foot;
        public Vector3 stepFrom;
        public Vector3 stepTo;
        public float stepProgress = 1f;
        public bool raised;
        public bool Planted => stepProgress >= 1f;
    }

    Leg[] legs;
    Transform bodyPivot;

    Vector3 wanderOrigin;
    Vector3 wanderTarget;
    int lastFrame = -1;

    // Ráfagas: alterna entre moverse un rato corto y quedarse tiesa, con duraciones
    // y velocidades sorteadas cada vez. Nunca dos tramos iguales.
    float burstTimer;
    float restTimer;

    // Tempo global: afecta tanto al deambular como a la caza, y también a qué tan
    // rápido plantan las patas. Cambia de golpe en momentos impredecibles.
    float tempoScale = 1f;
    float tempoTimer;
    float currentStepDuration;
    float fidgetTimer;

    // Caza
    Vector3 lastHeardPosition;
    float alertTimer;
    float startleTimer;
    bool wasAlert;

    float stunTimer;
    float distractionTimer;
    Vector3 retreatDirection;
    float retreatTimer;

    public bool IsRetreating => retreatTimer > 0f;
    public bool IsHunting => alertTimer > 0f;
    public bool IsStunned => stunTimer > 0f;

    // Mientras dura la secuencia de captura, la araña deja de moverse por su cuenta:
    // la posiciona SpiderCatchSequence.
    public bool IsCapturing { get; private set; }

    // Mientras corre el aviso de detección se queda clavada en el lugar: si avanzara
    // durante la cinemática te estaría ganando terreno mientras no puedes reaccionar.
    public bool FrozenByCinematic { get; set; }

    public void BeginCapture()
    {
        IsCapturing = true;
        SetThreatPose(true);
    }

    public void EndCapture()
    {
        IsCapturing = false;
        SetThreatPose(false);
    }

    // Todas las arañas vivas, para poder separarlas entre sí cuando convergen.
    public static readonly List<SpiderCreature> All = new List<SpiderCreature>();

    // La manda hacia un punto concreto, ignorando el ruido del jugador. La bengala
    // reparte a cada una un punto distinto alrededor del fogonazo, no el centro
    // exacto: si todas fueran al mismo sitio quedarían encimadas.
    public void Distract(Vector3 point, float duration)
    {
        lastHeardPosition = point;
        alertTimer = duration;
        distractionTimer = duration;
        startleTimer = 0f;
        retreatTimer = 0f;
        wasAlert = true;
    }

    // Corral alrededor del fogonazo. Mientras dura, la araña no puede salirse de ese
    // radio por más que la separación la empuje.
    //
    // Hace falta porque al llegar deja de cazar (se planta a un metro del destino) y ahí
    // ya nada la sujeta: la separación seguía empujándola contra las vecinas sin nada
    // que la trajera de vuelta, y terminaban derivando hasta el borde del círculo de luz
    // o fuera de él. Se veían cerca de la bengala, no atraídas por ella.
    public void ConfineTo(Vector3 center, float radius)
    {
        confineCenter = center;
        confineRadius = Mathf.Max(0.5f, radius);
        confined = false;
    }

    Vector3 confineCenter;
    float confineRadius;
    bool confined;

    void ApplyConfinement()
    {
        if (distractionTimer <= 0f || confineRadius <= 0f) return;

        Vector3 delta = transform.position - confineCenter;
        delta.y = 0f;
        float distance = delta.magnitude;

        // El corral se cierra recién cuando entró por sus propios medios. Aplicado
        // desde el fogonazo, una araña llamada a cincuenta unidades aparecería de golpe
        // pegada al borde en vez de caminar hasta ahí.
        if (!confined)
        {
            if (distance > confineRadius) return;
            confined = true;
            return;
        }

        if (distance <= confineRadius) return;

        Vector3 clamped = confineCenter + delta.normalized * confineRadius;
        transform.position = new Vector3(clamped.x, transform.position.y, clamped.z);
    }

    // Retirada dirigida: camina firme en una dirección concreta durante un rato,
    // sin las ráfagas del deambular. La usa el aviso de "te perdió" y va a servir
    // igual para que las arañas se dispersen en direcciones distintas.
    public void Retreat(Vector3 direction, float duration)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) direction = transform.forward;

        // Se suelta el corral de la bengala: la retirada tiene que poder alejarse.
        confineRadius = 0f;

        retreatDirection = direction.normalized;
        retreatTimer = duration;
        alertTimer = 0f;
        wasAlert = false;
    }

    public void Stun(float duration)
    {
        confineRadius = 0f;
        stunTimer = duration > 0f ? duration : stunDuration;
        alertTimer = 0f;
        wasAlert = false;
    }

    // Encabritada: las dos patas delanteras se despegan del suelo y quedan agitándose
    // en el aire. Es la diferencia entre una araña caminando y una amenazando.
    void SetThreatPose(bool on)
    {
        if (legs == null) return;
        for (int i = 0; i < legs.Length; i++)
            legs[i].raised = on && (i % legsPerSide == 0);
    }

    // Hay que llamarlo antes de que el objeto se active: Awake ya construye todo.
    public void SetMaterial(Material mat)
    {
        if (mat != null) bodyMaterial = mat;
    }

    void Awake()
    {
        BuildBody();
        BuildLegs();
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    // Empuje entre arañas: sin esto, varias yendo al mismo punto se meten una dentro
    // de otra y el resultado se ve roto. Es lo mínimo para que converger no se bugee.
    // Un solo rayo entre la cabeza de la araña y el origen del sonido, contra la capa de
    // los muros. Es la forma barata y estándar de resolver oclusión: no simula cómo el
    // sonido rodea un obstáculo —eso pide propagación por caminos y cuesta muchísimo
    // más—, pero para decidir "hay algo sólido en el medio" alcanza y sobra.
    //
    // La capa importa: si esto chocara contra todo, cada árbol y cada roca del scatter te
    // escondería, y el mapa entero pasaría a ser cobertura.
    bool Occluded(Vector3 soundOrigin)
    {
        Vector3 ear = transform.position + Vector3.up * earHeight;
        return Physics.Linecast(ear, soundOrigin, CoverWell.Mask, QueryTriggerInteraction.Ignore);
    }

    void ApplySeparation()
    {
        Vector3 push = Vector3.zero;
        int neighbours = 0;

        foreach (var other in All)
        {
            if (other == null || other == this) continue;

            Vector3 delta = transform.position - other.transform.position;
            delta.y = 0f;
            float d = delta.magnitude;
            if (d > separationRadius || d < 0.001f) continue;

            push += delta.normalized * (1f - d / separationRadius);
            neighbours++;
        }

        if (neighbours == 0) return;
        transform.position += push / neighbours * separationStrength * Time.deltaTime;
    }

    // Fuera de los pozos.
    //
    // Se saca de golpe y no empujando poco a poco: un empuje suave la dejaría medio
    // metida en la piedra mientras persigue, que es justo lo que se ve mal. Puesta al
    // borde exacto, rodea el brocal en vez de atravesarlo.
    //
    // Solo en el plano. La altura la resuelve después el apoyo de las patas contra el
    // suelo, y tocarla acá le pelearía a eso.
    void KeepOutOfWells()
    {
        var pozos = CoverWell.Todos;

        for (int i = 0; i < pozos.Count; i++)
        {
            Vector3 delta = transform.position - pozos[i].centro;
            delta.y = 0f;

            float minimo = pozos[i].radio + wellClearance;
            float d = delta.magnitude;
            if (d >= minimo) continue;

            // Justo en el centro no hay dirección que valga: se elige una cualquiera
            // antes que dividir por cero.
            Vector3 haciaFuera = d < 0.001f ? transform.forward : delta / d;

            Vector3 fuera = pozos[i].centro + haciaFuera * minimo;
            transform.position = new Vector3(fuera.x, transform.position.y, fuera.z);
        }
    }

    void Start()
    {
        wanderOrigin = transform.position;
        PickNewTarget();

        // Los pies arrancan ya apoyados en su sitio, si no la primera vuelta
        // da un tirón feo mientras las ocho patas corrigen a la vez.
        foreach (var leg in legs)
            leg.foot = GroundedRestPosition(leg);
    }

    void Update()
    {
        if (stunTimer > 0f) stunTimer -= Time.deltaTime;

        if (!IsCapturing)
        {
            UpdateHearing();
            UpdateTempo();

            if (FrozenByCinematic || IsStunned)
            {
                // Congelada o aturdida: sigue habiendo tics de patas, pero no avanza ni caza.
            }
            else if (IsRetreating) UpdateRetreat();
            else if (startleTimer > 0f) startleTimer -= Time.deltaTime;
            else if (IsHunting) UpdateHunt();
            else UpdateErraticWander();

            if (!FrozenByCinematic) ApplySeparation();

            // Después de la separación, no antes: es justamente la separación la que
            // las sacaba del círculo de la bengala.
            ApplyConfinement();

            // Lo último de todo. Separación y confinamiento pueden dejarla dentro de un
            // pozo, así que si esto corriera antes lo desharían y volvería a colarse por
            // la piedra. Al final, nadie puede volver a meterla.
            if (!FrozenByCinematic && !IsCapturing) KeepOutOfWells();

            // Fuera de la cadena: si la tienes encima te atrapa, esté cazándote o no.
            // Antes solo pasaba mientras cazaba, así que quedarse pegado a una que
            // te había perdido el rastro no hacía absolutamente nada.
            if (!FrozenByCinematic && !IsStunned && startleTimer <= 0f)
                TryCatchPlayer();
        }

        UpdateStepping();
        UpdateFidget();

        int frame = Mathf.FloorToInt(Time.time * frameRate);
        if (frame == lastFrame) return;
        lastFrame = frame;

        ApplyBodyPose();
        foreach (var leg in legs) SolveLeg(leg);
    }

    void TryCatchPlayer()
    {
        var noise = NoiseEmitter.Instance;
        if (noise == null) return;

        // En el plano, no en 3D. El cuerpo de la araña flota a 1,75 sobre el suelo y el
        // origen del jugador está en los pies, así que la distancia en 3D arrastraba esa
        // altura siempre: de un radio de 2,3 quedaban 1,49 reales de alcance horizontal.
        // Con la araña encima seguía sin alcanzar a agarrarte.
        // Metido en el pozo eres intocable. Punto.
        //
        // Es lo primero que se comprueba y no hay excepción por estar persiguiéndote: el
        // escondite es un refugio de verdad, no un sitio donde tienes menos posibilidades.
        // Una araña ciega no tiene forma honesta de encontrarte dentro de un brocal de
        // piedra, y dejarle un resquicio convierte la mecánica en una lotería — te
        // escondes bien y a veces mueres igual, que es lo que más rabia da de un juego
        // de sigilo.
        //
        // Y no se puede acampar dentro: ahí no recoges fichas ni llegas al módulo, así
        // que quedarse no gana nada. El coste de esconderse es el tiempo.
        if (PlayerHiding.Instance != null && PlayerHiding.Instance.IsHiding) return;

        Vector3 delta = noise.transform.position - transform.position;
        delta.y = 0f;
        if (delta.magnitude > catchRadius) return;

        SpiderCatchSequence.Instance?.TryCatch(this);
    }

    void UpdateHearing()
    {
        alertTimer -= Time.deltaTime;
        if (distractionTimer > 0f) distractionTimer -= Time.deltaTime;

        // Mientras dura la distracción de la bengala, tu ruido se ignora. Sin esto el
        // destino se sobrescribía con tu posición cada frame y la bengala no llegaba
        // a mover a nadie ni un paso.
        var noise = distractionTimer > 0f ? null : NoiseEmitter.Instance;
        if (noise != null)
        {
            float distance = Vector3.Distance(transform.position, noise.transform.position);

            // Con un muro de por medio te sigue oyendo, pero mucho menos lejos. Es el
            // modelo que usan los sistemas de percepción documentados —Unreal tiene
            // literalmente dos rangos de audición, uno con línea despejada y otro sin
            // ella— y se elige así por una razón concreta: cortar la audición del todo
            // deja al bicho tonto y explotable, porque bastaría un muro de un metro a un
            // metro de distancia para volverse intocable. Atenuar mantiene la tensión.
            float radio = noise.NoiseRadius * HearingScale;
            if (Occluded(noise.SoundOrigin)) radio *= occludedHearing;

            if (distance <= radio)
            {
                lastHeardPosition = noise.transform.position;
                alertTimer = alertMemory;

                // Al pasar de tranquila a alerta se queda tiesa un momento. Ese
                // instante de quietud antes de arrancar es lo que da el susto.
                if (!wasAlert) startleTimer = startleDuration;
            }
        }

        bool huntingNow = IsHunting;

        // Los avisos se disparan en los dos bordes: cuando pasa a cazarte y cuando
        // se le acaba la memoria y te pierde.
        if (huntingNow && !wasAlert) SpiderReactionSequence.Instance?.PlayDetected(this);
        else if (!huntingNow && wasAlert) SpiderReactionSequence.Instance?.PlayLostTrack(this);

        wasAlert = huntingNow;
    }

    // Cambio de tempo instantáneo, sin interpolar: pasa de arrastrarse a dispararse
    // sin nada en el medio. Interpolar acá arruinaría todo el efecto.
    void UpdateTempo()
    {
        tempoTimer -= Time.deltaTime;
        if (tempoTimer > 0f) return;

        if (Random.value < twitchChance)
        {
            tempoScale = twitchSpeed;
            tempoTimer = Random.Range(twitchDuration.x, twitchDuration.y);
        }
        else
        {
            tempoScale = Random.Range(tempoSpeedRange.x, tempoSpeedRange.y);
            tempoTimer = Random.Range(tempoInterval.x, tempoInterval.y);
        }

        // Las patas siguen al tempo, pero con su propio desorden encima: si plantaran
        // exactamente al ritmo del cuerpo se volvería a ver como un ciclo regular.
        currentStepDuration = stepDuration / Mathf.Max(0.45f, tempoScale) * Random.Range(0.7f, 1.45f);
    }

    // Reacomoda una pata al azar sin moverse del sitio. Es el tic que más delata
    // que es un bicho y no algo que se desliza por el suelo.
    void UpdateFidget()
    {
        fidgetTimer -= Time.deltaTime;
        if (fidgetTimer > 0f) return;
        fidgetTimer = Random.Range(fidgetInterval.x, fidgetInterval.y);

        int start = Random.Range(0, legs.Length);
        for (int i = 0; i < legs.Length; i++)
        {
            var leg = legs[(start + i) % legs.Length];
            if (!leg.Planted) continue;

            leg.stepFrom = leg.foot;
            leg.stepTo = leg.foot + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
            leg.stepProgress = 0f;
            return;
        }
    }

    void UpdateRetreat()
    {
        retreatTimer -= Time.deltaTime;

        Quaternion want = Quaternion.LookRotation(retreatDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * 1.4f * Time.deltaTime);
        transform.position += transform.forward * moveSpeed * tempoScale * Time.deltaTime;

        // Al terminar de irse, retoma el deambular desde donde quedó y no desde
        // el punto de origen, si no volvería derecho hacia donde estabas.
        if (retreatTimer <= 0f)
        {
            wanderOrigin = transform.position;
            PickNewTarget();
        }
    }

    void UpdateHunt()
    {
        Vector3 flatTarget = new Vector3(lastHeardPosition.x, transform.position.y, lastHeardPosition.z);
        Vector3 toTarget = flatTarget - transform.position;

        // Se planta a un metro del punto, no a metro y medio. Suena a nada, pero sobre
        // una bengala la diferencia es entre quedarse encima y quedarse al lado.
        if (toTarget.sqrMagnitude < 1f) return;

        Quaternion want = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

        // Cuanto más cerca, más rápido gira. De lejos el giro lento es lo que la hace
        // ver bicho; de cerca era el bug: al alcanzarte de costado o por la espalda
        // tenía que girar media vuelta, y como solo avanza hacia su propio frente,
        // durante ese giro se alejaba describiendo un círculo. Nunca terminaba de
        // entrar en el radio de agarre y quedaba orbitando alrededor tuyo sin hacer
        // nada. De frente no pasaba porque ya venía encarada.
        float closeness = 1f - Mathf.Clamp01(toTarget.magnitude / 6f);
        float turn = turnSpeed * Mathf.Lerp(1.6f, 6f, closeness);

        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turn * Time.deltaTime);
        transform.position += transform.forward * huntSpeed * HuntScale * tempoScale * Time.deltaTime;
    }

    void UpdateErraticWander()
    {
        if (restTimer > 0f)
        {
            restTimer -= Time.deltaTime;
            return;
        }

        if (burstTimer > 0f)
        {
            burstTimer -= Time.deltaTime;
            MoveDuringBurst();

            // Al terminar la ráfaga se queda quieta un rato antes de la siguiente.
            if (burstTimer <= 0f)
                restTimer = Random.Range(pauseDuration.x, pauseDuration.y);
            return;
        }

        StartNewBurst();
    }

    void MoveDuringBurst()
    {
        Vector3 flatTarget = new Vector3(wanderTarget.x, transform.position.y, wanderTarget.z);
        Vector3 toTarget = flatTarget - transform.position;
        if (toTarget.sqrMagnitude < 4f)
        {
            PickNewTarget();
            return;
        }

        Quaternion want = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
        transform.position += transform.forward * moveSpeed * tempoScale * Time.deltaTime;
    }

    void StartNewBurst()
    {
        burstTimer = Random.Range(burstDuration.x, burstDuration.y);

        // Desvío brusco al arrancar: es lo que rompe la trayectoria en línea recta
        // y hace que no se le vea un patrón.
        transform.rotation *= Quaternion.Euler(0f, Random.Range(-burstTurnJitter, burstTurnJitter), 0f);

        if (Random.value < 0.35f) PickNewTarget();
    }

    void PickNewTarget()
    {
        Vector2 offset = Random.insideUnitCircle * wanderRadius;
        wanderTarget = wanderOrigin + new Vector3(offset.x, 0f, offset.y);
    }

    void UpdateStepping()
    {
        int stepping = 0;
        foreach (var leg in legs)
            if (!leg.Planted) stepping++;

        foreach (var leg in legs)
        {
            // Las patas alzadas no pisan: se quedan agitándose en el aire.
            if (leg.raised)
            {
                leg.foot = RaisedFootPosition(leg);
                leg.stepProgress = 1f;
                continue;
            }

            if (leg.Planted)
            {
                if (stepping >= maxSimultaneousSteps) continue;

                Vector3 desired = GroundedRestPosition(leg);
                if (Vector3.Distance(leg.foot, desired) < stepThreshold) continue;

                leg.stepFrom = leg.foot;
                // Se pisa un poco más allá del sitio ideal: así el pie queda "adelantado"
                // y no tiene que volver a pasar de inmediato.
                leg.stepTo = desired + (desired - leg.foot).normalized * 0.35f;
                leg.stepProgress = 0f;
                stepping++;
            }
            else
            {
                leg.stepProgress += Time.deltaTime / Mathf.Max(0.05f, currentStepDuration);
                float t = Mathf.Clamp01(leg.stepProgress);
                leg.foot = Vector3.Lerp(leg.stepFrom, leg.stepTo, t)
                           + Vector3.up * Mathf.Sin(t * Mathf.PI) * stepHeight;
            }
        }
    }

    Vector3 GroundedRestPosition(Leg leg)
    {
        Vector3 world = transform.TransformPoint(leg.restLocal);
        if (Physics.Raycast(world + Vector3.up * 4f, Vector3.down, out var hit, 10f))
            return hit.point;
        return world;
    }

    Vector3 RaisedFootPosition(Leg leg)
    {
        Vector3 lifted = leg.restLocal + new Vector3(0f, bodyHeight + 1.6f, 1.1f);
        Vector3 world = transform.TransformPoint(lifted);

        // Agitación: si quedaran clavadas en el aire se verían de plástico.
        float seed = leg.restLocal.x * 3.1f;
        world += new Vector3(
            (Mathf.PerlinNoise(Time.time * 3.4f, seed) - 0.5f) * 0.75f,
            (Mathf.PerlinNoise(seed, Time.time * 3.9f) - 0.5f) * 0.6f,
            (Mathf.PerlinNoise(Time.time * 2.8f, seed + 7f) - 0.5f) * 0.5f);
        return world;
    }

    void ApplyBodyPose()
    {
        // El cuerpo flota sobre el promedio de los pies y se inclina apenas según el
        // desnivel entre lados. Es sutil, pero sin eso parece una caja que se desliza.
        // Solo cuenta el promedio de las patas apoyadas: si entraran las alzadas,
        // encabritarse levantaría el cuerpo el doble de lo que corresponde.
        float avgY = 0f;
        int planted = 0;
        foreach (var leg in legs)
        {
            if (leg.raised) continue;
            avgY += leg.foot.y;
            planted++;
        }
        avgY = planted > 0 ? avgY / planted : transform.position.y - bodyHeight;

        float targetHeight = avgY + bodyHeight + (IsCapturing ? threatBodyLift : 0f);

        // Durante la captura la posición horizontal la maneja la secuencia; acá solo
        // se ajusta la altura, si no las dos cosas se pelearían por el transform.
        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetHeight, Time.deltaTime * 6f);
        transform.position = pos;

        // Ruido y no seno: un vaivén perfectamente periódico se lee mecánico.
        // Y se agita más cuanto más rápido va, así el tempo también se ve en el cuerpo.
        float agitation = IsStunned ? 0.25f : Mathf.Lerp(0.6f, 2.2f, Mathf.Clamp01(tempoScale / 2.6f));
        float pitch = (Mathf.PerlinNoise(Time.time * 1.9f, 0.3f) - 0.5f) * 7f * agitation;
        float roll = (Mathf.PerlinNoise(0.7f, Time.time * 1.5f) - 0.5f) * 8f * agitation;

        // Encabritada, el cuerpo se echa hacia atrás para presentar las patas delanteras.
        if (IsCapturing) pitch -= 26f;
        else if (IsStunned) pitch += 12f;

        bodyPivot.localRotation = Quaternion.Euler(pitch, 0f, roll);
    }

    // IK de dos huesos: con la longitud del fémur, la de la tibia y la distancia
    // hasta el pie, la ley del coseno da el ángulo de la rodilla. Se fuerza a que
    // la rodilla quede por encima de la línea cadera-pie, que es como dobla una araña.
    void SolveLeg(Leg leg)
    {
        Vector3 hip = transform.TransformPoint(leg.hipLocal);
        Vector3 toFoot = leg.foot - hip;
        float d = Mathf.Clamp(toFoot.magnitude, 0.05f, femurLength + tibiaLength - 0.01f);
        Vector3 dir = toFoot.normalized;

        float cosHip = (femurLength * femurLength + d * d - tibiaLength * tibiaLength) / (2f * femurLength * d);
        float hipAngle = Mathf.Acos(Mathf.Clamp(cosHip, -1f, 1f));

        Vector3 axis = Vector3.Cross(dir, Vector3.up);
        if (axis.sqrMagnitude < 0.0001f) axis = Vector3.Cross(dir, Vector3.forward);
        axis.Normalize();

        Vector3 perp = Vector3.Cross(axis, dir).normalized;
        if (perp.y < 0f) perp = -perp;

        Vector3 knee = hip + (dir * Mathf.Cos(hipAngle) + perp * Mathf.Sin(hipAngle)) * femurLength;

        PlaceBone(leg.femur, hip, knee, femurLength);
        PlaceBone(leg.tibia, knee, leg.foot, tibiaLength);
    }

    void PlaceBone(Transform bone, Vector3 from, Vector3 to, float length)
    {
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < 0.0001f) return;

        bone.position = (from + to) * 0.5f;
        bone.up = delta.normalized;
        bone.localScale = new Vector3(legThickness, length, legThickness);
    }

    void BuildBody()
    {
        var mat = bodyMaterial != null ? bodyMaterial : FallbackMaterial();

        var pivotGO = new GameObject("Cuerpo");
        pivotGO.transform.SetParent(transform, false);
        bodyPivot = pivotGO.transform;

        var abdomen = new GameObject("Abdomen");
        abdomen.transform.SetParent(bodyPivot, false);
        abdomen.transform.localPosition = new Vector3(0f, 0f, -bodyLength * 0.45f);
        abdomen.AddComponent<MeshFilter>().sharedMesh = LowPolyMeshFactory.CreateCrystal(7, 0.8f, 0.52f, 11);
        abdomen.AddComponent<MeshRenderer>().sharedMaterial = mat;
        abdomen.transform.localScale = new Vector3(1f, 0.75f, 1.25f);

        var thorax = new GameObject("Cefalotorax");
        thorax.transform.SetParent(bodyPivot, false);
        thorax.transform.localPosition = new Vector3(0f, 0.02f, bodyLength * 0.35f);
        thorax.AddComponent<MeshFilter>().sharedMesh = LowPolyMeshFactory.CreateCrystal(6, 0.55f, 0.34f, 23);
        thorax.AddComponent<MeshRenderer>().sharedMaterial = mat;
        thorax.transform.localScale = new Vector3(1f, 0.7f, 1.1f);
    }

    void BuildLegs()
    {
        var mat = bodyMaterial != null ? bodyMaterial : FallbackMaterial();
        var legRoot = new GameObject("Patas");
        legRoot.transform.SetParent(transform, false);

        legs = new Leg[legsPerSide * 2];
        int index = 0;

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < legsPerSide; i++)
            {
                float along = legsPerSide > 1 ? Mathf.Lerp(0.5f, -0.5f, i / (float)(legsPerSide - 1)) : 0f;
                float z = along * bodyLength;

                var leg = new Leg
                {
                    hipLocal = new Vector3(side * 0.22f, 0f, z),
                    restLocal = new Vector3(side * legReach, -bodyHeight, z * legSpreadZ),
                    femur = MakeBone(legRoot.transform, $"Femur_{side}_{i}", mat),
                    tibia = MakeBone(legRoot.transform, $"Tibia_{side}_{i}", mat),
                };

                legs[index++] = leg;
            }
        }
    }

    // Sin collider a propósito: las patas lanzan raycasts al suelo para saber dónde
    // pisar, y si los huesos tuvieran collider se detectarían a sí mismos.
    static Mesh boneMesh;

    Transform MakeBone(Transform parent, string name, Material mat)
    {
        // El cubo se saca de una primitiva y no de Resources.GetBuiltinResource, por el
        // mismo motivo que la fuente: esa llamada devuelve null en el juego compilado
        // cuando nada referencia el recurso, y la araña se quedaría sin cuerpo ni patas
        // sin dar ningún error. CreatePrimitive siempre trae su malla.
        if (boneMesh == null)
        {
            var molde = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boneMesh = molde.GetComponent<MeshFilter>().sharedMesh;
            Destroy(molde);
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = boneMesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    static Material FallbackMaterial()
    {
        var mat = new Material(Shader.Find("Cielo Profundo/PS1 Vertex Snap"));
        mat.SetColor("_BaseColor", new Color(0.09f, 0.07f, 0.11f));
        mat.SetFloat("_SnapResolution", 140f);
        return mat;
    }
}
