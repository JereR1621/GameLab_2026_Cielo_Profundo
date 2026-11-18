using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Estudio previo al descenso: la esfera armilar de Paralax.
//
// Tres anillos concéntricos de vidrio grabado, uno sobre otro. Entre los tres llevan
// una constelación repartida: las estrellas y los tramos de línea que las unen quedan
// en el anillo que corresponde a su radio. Con los anillos girados, las líneas se
// cortan en los bordes y no hay figura. Alineados, los tramos se encuentran y la
// constelación aparece entera.
//
// Cada anillo tiene su color, como los haces de la iglesia del RE4. No es adorno: es
// lo que te deja ver de un vistazo qué trazo pertenece a qué anillo, y sobre todo qué
// arrastra cada palanca cuando el instrumento viene engranado. Al resolverse los tres
// colores se funden en uno — la figura deja de ser tres pedazos y pasa a ser una.
//
// Desde el tramo 2 el instrumento viene engranado: ya no giras un anillo, giras una
// palanca que arrastra dos a la vez. Cada arreglo desarma algo que ya tenías.
public class ArmillarySurveyUI : HoloPanel
{
    [SerializeField] string targetSceneName = "Planet_Test_Procedural";

    [Header("Dificultad por intento")]
    [Tooltip("Posiciones de giro por vuelta. Más posiciones, ajuste más fino.")]
    [SerializeField] int[] positionsPerAttempt = { 8, 10, 12 };

    [Tooltip("Movimientos de desorden. Se parte del estado resuelto y se desarma con estos, así que siempre tiene solución en esa misma cantidad.")]
    [SerializeField] int[] scramblePerAttempt = { 3, 4, 6 };

    [Tooltip("Cuántos anillos participan. Con 2 el tercero queda fijo y decorativo.")]
    [SerializeField] int[] ringsPerAttempt = { 2, 3, 3 };

    // Reloj en LOS TRES intentos, y esto no es un ajuste: es lo que hace que el sistema
    // de intentos exista.
    //
    // Antes solo el tercero tenía reloj, y como el puzle se acepta solo en cuanto encaja,
    // los dos primeros no se podían perder de ninguna forma… salvo apretando E, que era
    // la única tecla capaz de fallar y encima estaba anunciada en el pie. O sea que todo
    // el castigo de bajar sin identificar dependía de que el jugador se equivocara
    // haciendo caso a lo que le decía la pantalla.
    //
    // Ahora se pierde por el reloj y solo por el reloj, que además es lo que dice la
    // ficción: una ventana de observación que se cierra.
    [Tooltip("Segundos por intento. Cero o menos = sin límite.")]
    [SerializeField] float[] timePerAttempt = { 90f, 75f, 55f };

    [Header("Instrumento")]
    [SerializeField] float outerRadius = 210f;
    [SerializeField] float starSize = 8f;
    [SerializeField] float lineThickness = 3.5f;
    [Tooltip("Largo de cada tramo en que se parte una línea. Corto = el corte en el borde del anillo se nota más.")]
    [SerializeField] float segmentLength = 10f;

    [Header("Colores de los anillos")]
    [SerializeField] Color innerColor = new Color(0.38f, 0.86f, 0.94f);   // cian
    [SerializeField] Color middleColor = new Color(0.98f, 0.63f, 0.30f);  // ámbar
    [SerializeField] Color outerColor = new Color(0.76f, 0.56f, 0.98f);   // violeta
    [Tooltip("Color al que se funden los tres cuando la figura cierra.")]
    [SerializeField] Color solvedColor = new Color(0.90f, 0.99f, 1f);

    [Tooltip("Cuánto se apagan los anillos que no estás controlando. Bajo, pero nunca cero: la figura hay que seguir viéndola entera para saber dónde falta cerrar.")]
    [SerializeField, Range(0.15f, 1f)] float dimUnselected = 0.42f;

    [Header("Castigo")]
    [SerializeField] float blindDifficulty = 2f;

    // ── Estado ──────────────────────────────────────────────────────────────────
    const int RingCount = 3;

    RectTransform dial;
    readonly RectTransform[] rings = new RectTransform[RingCount];
    readonly int[] ringStep = new int[RingCount];
    readonly List<Image>[] guides = new List<Image>[RingCount];
    readonly List<Image>[] marks = new List<Image>[RingCount];

    // Opacidad con la que se dibujó cada trazo. Hay que guardarla aparte porque el
    // repintado cambia color Y brillo a la vez: leyendo la opacidad del propio trazo,
    // cada repintado se apoyaría en el anterior y la figura se iría apagando sola.
    readonly List<float>[] markAlpha = new List<float>[RingCount];

    RectTransform preview, controlList, guideRoot;
    RectTransform arrowLeft, arrowRight;
    readonly List<Image> arrowImages = new List<Image>();
    Text header, status, footer, goal, timerText;

    readonly List<Image> rowHighlights = new List<Image>();
    readonly List<Text> rowLabels = new List<Text>();
    readonly List<GameObject> rowObjects = new List<GameObject>();

    Vector3 lastPointer;
    int positions = 8;
    int activeRings = 2;
    int selection;
    bool geared;
    readonly List<int[]> couplings = new List<int[]>();

    int attempt;
    float timeLeft;
    bool resolved;
    bool compliant;

    Color RingColor(int ring) => ring switch
    {
        0 => innerColor,
        1 => middleColor,
        _ => outerColor,
    };

    void Awake()
    {
        for (int i = 0; i < RingCount; i++)
        {
            guides[i] = new List<Image>();
            marks[i] = new List<Image>();
            markAlpha[i] = new List<float>();
        }

        BuildUI();
        panelRoot.SetActive(false);
    }

    protected override void OnOpened()
    {
        attempt = Mathf.Clamp(Expedition.FailedSurveys, 0, 2);
        resolved = false;
        StartAttempt();
    }

    // ── Ciclo ───────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!IsOpen || resolved) return;

        if (timeLeft > 0f)
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
            {
                Fail("La ventana de observación se cerró.");
                return;
            }

            RefreshTimer();
        }

        if (!InputReady) return;

        int previous = selection;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) selection++;
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) selection--;

        // Ratón. Se puede elegir por la lista lateral o, y esto es lo importante,
        // poniendo el puntero SOBRE el anillo que se quiere girar.
        //
        // El puzle trata de girar anillos, y el primer gesto de cualquiera es ir al
        // anillo. Antes el disco era solo pantalla y todo el mando pasaba por una lista
        // de texto al costado, que es justo donde nadie mira primero.
        bool moved = HoloUI.PointerMovedSince(ref lastPointer);

        int over = HoloUI.RowAtPointer(rowHighlights);
        if (over < 0) over = ControlAtPointer();
        if (over >= 0 && (moved || Input.GetMouseButtonDown(0))) selection = over;

        selection = Mathf.Clamp(selection, 0, ControlCount - 1);
        if (selection != previous) { UISound.Move(); Repaint(); }

        // La rueda gira. Es el gesto del dial, y con el puntero encima del anillo la
        // cosa se explica sola: apuntas y ruedas.
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f) Apply(selection, wheel > 0f ? +1 : -1);

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Apply(selection, +1);
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Apply(selection, -1);

        // Ya no hay tecla de confirmar. El puzle se acepta solo en cuanto la figura
        // cierra, así que confirmar solo podía servir para fallar.
        if (Input.GetKeyDown(KeyCode.Escape)) { UISound.Click(); Close(); }
    }

    // Qué control corresponde al anillo que hay bajo el puntero, o -1 si el puntero no
    // está sobre el disco.
    //
    // Engranado, un anillo lo mueven dos palancas: se elige la primera que lo toca. No
    // hay una respuesta única, pero cualquiera de las dos es mejor que ninguna, y al
    // resaltarse se ve enseguida qué arrastra.
    int ControlAtPointer()
    {
        if (dial == null) return -1;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dial, Input.mousePosition, null, out Vector2 local))
            return -1;

        float r = local.magnitude;
        if (r > outerRadius) return -1;

        int ring = Mathf.Clamp(Mathf.FloorToInt(r / (outerRadius / activeRings)), 0, activeRings - 1);

        if (!geared) return ring;

        for (int i = 0; i < couplings.Count; i++)
            for (int c = 0; c < couplings[i].Length; c += 2)
                if (couplings[i][c] == ring) return i;

        return -1;
    }

    int ControlCount => geared ? couplings.Count : activeRings;

    void Apply(int control, int direction)
    {
        if (geared)
        {
            var coupling = couplings[Mathf.Clamp(control, 0, couplings.Count - 1)];
            for (int i = 0; i < coupling.Length; i += 2)
                Rotate(coupling[i], direction * coupling[i + 1]);
        }
        else
        {
            Rotate(control, direction);
        }

        UISound.Move();
        Repaint();
        CheckSolved();
    }

    void Rotate(int ring, int steps)
    {
        if (ring < 0 || ring >= activeRings) return;

        // PRL-000 no se deja desalinear. El instrumento devuelve el patrón que le
        // muestres, así que muevas lo que muevas la figura sigue entera.
        if (compliant) return;

        ringStep[ring] = ((ringStep[ring] + steps) % positions + positions) % positions;
        ApplyRingAngles();
    }

    void ApplyRingAngles()
    {
        float stepAngle = 360f / positions;
        for (int i = 0; i < RingCount; i++)
            rings[i].localRotation = Quaternion.Euler(0f, 0f, ringStep[i] * stepAngle);
    }

    bool IsSolved()
    {
        for (int i = 0; i < activeRings; i++)
            if (ringStep[i] != 0) return false;
        return true;
    }

    void CheckSolved()
    {
        if (!IsSolved()) return;

        resolved = true;
        Expedition.ResetSurvey();

        // Los tres colores se funden: la figura deja de ser tres pedazos.
        Repaint();

        status.text = compliant
            ? "COINCIDENCIA INMEDIATA  ·  SIN RESIDUO DE AJUSTE"
            : "FIGURA COMPLETA  ·  CUERPO IDENTIFICADO";
        footer.text = "DESCENSO AUTORIZADO";
        if (goal != null) goal.text = "";
        if (timerText != null) timerText.text = "";

        // PRL-000 se queda un poco más en pantalla. Ahí la figura ya estaba entera al
        // abrir y el jugador no ha hecho nada: hay que darle tiempo a leer que el
        // instrumento coincidió solo, que es justamente lo inquietante.
        Invoke(nameof(Descend), compliant ? 3f : 1.6f);
    }

    void Fail(string reason)
    {
        attempt++;
        Expedition.FailedSurveys = attempt;

        if (attempt >= 3)
        {
            resolved = true;
            Expedition.DescentDifficulty = blindDifficulty;
            status.text = "SIN IDENTIFICAR  ·  TRES MEDICIONES DESCARTADAS";
            footer.text = "DESCENSO A CIEGAS AUTORIZADO";
            Invoke(nameof(Descend), 2f);
            return;
        }

        StartAttempt();
        status.text = reason;
    }

    void Descend()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SceneManager.LoadScene(targetSceneName);
    }

    // ── Armado del intento ──────────────────────────────────────────────────────

    void StartAttempt()
    {
        var body = RouteDatabase.Current;

        positions = Table(positionsPerAttempt, 8);

        // Los engranajes aparecen en el tramo 2. Los primeros cuerpos se resuelven
        // anillo por anillo, que es lo que enseña a leer la figura antes de complicarla.
        geared = body != null && body.tramo >= 2;

        // Engranado siempre con los tres: las palancas arrastran el anillo exterior, y
        // con solo dos activos habría palancas que no mueven nada visible.
        activeRings = geared ? RingCount : Mathf.Clamp(Table(ringsPerAttempt, 3), 1, RingCount);
        compliant = body != null && body.id == "PRL-000";

        BuildCouplings();

        // Dos semillas, y esto importa más de lo que parece.
        //
        // La CONSTELACIÓN sale solo del cuerpo: es la figura de ese cuerpo y no cambia
        // aunque reintentes. Antes el número de intento entraba en la semilla, así que
        // cada reintento dibujaba un cielo distinto — y entonces no estabas identificando
        // nada, que es justo lo que dice que estás haciendo. Ahora reintentar es volver
        // al mismo cielo, y lo que aprendiste en el intento anterior sirve.
        //
        // El DESORDEN sí cambia por intento: repetir la misma posición de partida haría
        // que la segunda oportunidad fuera gratis.
        int constellationSeed = (body != null ? body.semilla : 1) * 977;
        int scrambleSeed = constellationSeed + attempt * 131;

        BuildRingGuides();
        BuildConstellation(constellationSeed);
        BuildTicks();

        for (int i = 0; i < RingCount; i++) ringStep[i] = 0;
        if (!compliant) Scramble(scrambleSeed);
        ApplyRingAngles();

        selection = 0;
        timeLeft = Table(timePerAttempt, 0f);

        header.text = $"ESFERA ARMILAR  —  INTENTO {attempt + 1} DE 3";
        status.text = body != null ? $"{body.nombre}  ·  {body.clase}" : "Cuerpo sin asignar";
        goal.text = "Gira los anillos hasta rehacer la figura de la carta de referencia.";

        BuildControlRows();
        Repaint();
        RefreshFooter();
        RefreshTimer();

        // PRL-000 abre con la figura ya entera y no se deja mover. Sin esto el jugador
        // se quedaría ahí apretando teclas sin que pasara nada, esperando un cierre que
        // nunca llegaba porque solo se comprobaba al girar algo.
        CheckSolved();
    }

    // Se desordena desde el estado resuelto con movimientos legales. Así el tablero
    // siempre tiene solución, y en exactamente esa cantidad de movimientos.
    void Scramble(int seed)
    {
        var rng = new System.Random(seed);
        int moves = Table(scramblePerAttempt, 4);

        for (int i = 0; i < moves; i++)
        {
            int control = rng.Next(0, ControlCount);
            int direction = rng.Next(0, 2) == 0 ? -1 : 1;

            if (geared)
            {
                var coupling = couplings[control];
                for (int c = 0; c < coupling.Length; c += 2)
                    RotateRaw(coupling[c], direction * coupling[c + 1]);
            }
            else
            {
                RotateRaw(control, direction);
            }
        }

        if (IsSolved()) RotateRaw(0, 1);
    }

    // Giro sin el candado de PRL-000: lo usa el desorden inicial, que corre antes de
    // que el jugador toque nada.
    void RotateRaw(int ring, int steps)
    {
        if (ring < 0 || ring >= activeRings) return;
        ringStep[ring] = ((ringStep[ring] + steps) % positions + positions) % positions;
    }

    // Cada palanca arrastra dos anillos, y en sentidos contrarios: son engranajes que
    // se muerden, no un eje común.
    void BuildCouplings()
    {
        couplings.Clear();
        if (!geared) return;

        // {anillo, sentido, anillo, sentido}
        couplings.Add(new[] { 0, +1, 1, -1 });
        couplings.Add(new[] { 1, +1, 2, -1 });
        couplings.Add(new[] { 0, +1, 2, +1 });

        // Ya no hay palanca oculta en el tercer intento. Una palanca que no dice qué
        // mueve es dificultad por ocultar información, no por complicar el problema, y
        // en una jam —donde el jugador tiene cinco minutos y ninguna paciencia— eso se
        // lee como que la interfaz está rota. El reloj ya sube la tensión sin mentir.
    }

    // ── La constelación ─────────────────────────────────────────────────────────

    // Se genera con la semilla del cuerpo: cada destino tiene su propia figura y
    // siempre la misma. Reintentar es volver al mismo cielo, no a otro.
    void BuildConstellation(int seed)
    {
        for (int i = 0; i < RingCount; i++)
        {
            Clear(rings[i]);
            marks[i].Clear();
            markAlpha[i].Clear();
        }
        Clear(preview);

        var rng = new System.Random(seed);

        var stars = new List<Vector2>();
        int wanted = 7 + rng.Next(0, 4);
        for (int guard = 0; guard < 400 && stars.Count < wanted; guard++)
        {
            float r = Mathf.Sqrt((float)rng.NextDouble()) * outerRadius * 0.92f;
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;

            bool ok = true;
            foreach (var s in stars)
                if (Vector2.Distance(s, p) < outerRadius * 0.28f) { ok = false; break; }

            if (ok) stars.Add(p);
        }

        // Se unen en cadena por vecino más cercano: da una figura de trazo continuo,
        // que es como se dibuja una constelación de verdad. Después una rama suelta.
        var edges = new List<(int, int)>();
        var pending = new List<int>();
        for (int i = 1; i < stars.Count; i++) pending.Add(i);

        int current = 0;
        while (pending.Count > 0)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < pending.Count; i++)
            {
                float d = Vector2.Distance(stars[current], stars[pending[i]]);
                if (d < bestDist) { bestDist = d; best = i; }
            }

            edges.Add((current, pending[best]));
            current = pending[best];
            pending.RemoveAt(best);
        }

        if (stars.Count > 4)
            edges.Add((rng.Next(0, stars.Count / 2), stars.Count - 1));

        foreach (var star in stars)
        {
            int ring = RingOf(star.magnitude);
            // Halo primero, estrella encima: le da cuerpo sin costar nada.
            AddMark(ring, AddDot(rings[ring], star, starSize * 2.6f, 0.16f));
            AddMark(ring, AddDot(rings[ring], star, starSize, 1f));
        }

        foreach (var (a, b) in edges)
        {
            Vector2 from = stars[a], to = stars[b];
            float length = Vector2.Distance(from, to);
            int pieces = Mathf.Max(1, Mathf.CeilToInt(length / segmentLength));

            for (int i = 0; i < pieces; i++)
            {
                Vector2 p0 = Vector2.Lerp(from, to, i / (float)pieces);
                Vector2 p1 = Vector2.Lerp(from, to, (i + 1) / (float)pieces);
                Vector2 mid = (p0 + p1) * 0.5f;
                int ring = RingOf(mid.magnitude);
                AddMark(ring, AddSegment(rings[ring], p0, p1, lineThickness, 0.85f));
            }
        }

        BuildPreview(stars, edges);
    }

    // Marcas de posición en el borde de cada anillo: dicen cuántas posiciones tiene y,
    // como giran con él, dejan ver el movimiento incluso donde no hay trazo.
    //
    // TODAS iguales, y esto es deliberado. Antes la del cero era más larga y más
    // brillante, y con el índice fijo arriba eso formaba un chivato: alineabas tres
    // rayitas bajo el puntero y listo, sin mirar la figura ni una vez. Era muchísimo
    // más fácil de leer que juzgar si los trazos se encuentran, así que cualquiera lo
    // descubría en diez segundos y la constelación pasaba a ser papel pintado — justo
    // lo contrario de la idea de la que salió esto.
    //
    // Iguales, los ticks siguen diciendo cuánto has girado, que es información útil, y
    // dejan de decir cuándo has acertado, que es la respuesta.
    void BuildTicks()
    {
        for (int ring = 0; ring < activeRings; ring++)
        {
            float radius = outerRadius * (ring + 1) / activeRings;

            for (int i = 0; i < positions; i++)
            {
                float a = i / (float)positions * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

                var p0 = dir * (radius - 8f);
                var p1 = dir * (radius - 2f);
                AddMark(ring, AddSegment(rings[ring], p0, p1, 2f, 0.4f));
            }
        }
    }

    // Referencia chica y apagada en la esquina: confirma qué estás armando sin
    // resolverlo por ti, porque no dice en qué posición va cada anillo.
    void BuildPreview(List<Vector2> stars, List<(int, int)> edges)
    {
        const float scale = 0.21f;

        foreach (var (a, b) in edges)
        {
            var seg = AddSegment(preview, stars[a] * scale, stars[b] * scale, 1.6f, 1f);
            seg.color = HoloUI.LineDim;
        }

        foreach (var star in stars)
        {
            var dot = AddDot(preview, star * scale, 4f, 1f);
            dot.color = HoloUI.Line;
        }
    }

    int RingOf(float radius)
    {
        // Con dos anillos activos el tercero no participa: su franja se reparte entre
        // los otros dos para que no quede una zona de la figura que nunca se mueve.
        float band = outerRadius / activeRings;
        return Mathf.Clamp(Mathf.FloorToInt(radius / band), 0, activeRings - 1);
    }

    // ── Color y selección ───────────────────────────────────────────────────────

    // Guarda un trazo junto con la opacidad con que se dibujó.
    void AddMark(int ring, Image image)
    {
        marks[ring].Add(image);
        markAlpha[ring].Add(image.color.a);
    }

    // Un único repintado que decide color Y brillo a la vez.
    //
    // Antes eran dos pasadas separadas —una para el color del anillo y otra para el
    // resaltado— y cada una leía la opacidad que había dejado la otra, así que se
    // pisaban. Con la opacidad original guardada aparte, cada repintado parte siempre
    // del mismo sitio.
    //
    // Lo que hace por dentro es lo que responde a "¿esto se puede girar?": el anillo que
    // controlas se enciende entero —estrellas, trazos y circunferencia— y los demás se
    // apagan. No hay que leer nada para saber qué estás moviendo.
    void Repaint()
    {
        var touched = new bool[RingCount];

        if (geared && selection < couplings.Count)
        {
            var coupling = couplings[selection];
            for (int i = 0; i < coupling.Length; i += 2) touched[coupling[i]] = true;
        }
        else if (!geared && selection < activeRings)
        {
            touched[selection] = true;
        }

        for (int ring = 0; ring < RingCount; ring++)
        {
            // Resuelto se enciende todo y del mismo color: la figura deja de ser tres
            // pedazos de tres dueños y pasa a ser una sola cosa.
            Color color = resolved ? solvedColor : RingColor(ring);
            float brillo = resolved || touched[ring] ? 1f : dimUnselected;

            for (int i = 0; i < marks[ring].Count; i++)
            {
                var image = marks[ring][i];
                if (image == null) continue;
                image.color = new Color(color.r, color.g, color.b, markAlpha[ring][i] * brillo);
            }

            float guideAlpha = resolved ? 0.5f : touched[ring] ? 0.55f : 0.1f;
            foreach (var guide in guides[ring])
                if (guide != null) guide.color = new Color(color.r, color.g, color.b, guideAlpha);
        }

        RefreshArrows(touched);

        for (int i = 0; i < rowHighlights.Count; i++)
        {
            bool active = i == selection;
            rowHighlights[i].color = active
                ? new Color(0.42f, 0.84f, 0.92f, 0.16f)
                : Color.clear;
            rowLabels[i].color = active ? HoloUI.Text : HoloUI.TextDim;
        }
    }

    // Dos flechas pegadas al anillo que controlas, una a cada lado.
    //
    // Es la respuesta directa a "¿cómo sé que esto gira?". El disco es un dibujo quieto:
    // nada en él dice que sea manipulable. Dos puntas apuntando a izquierda y derecha
    // sobre el anillo encendido lo dicen sin texto, y de paso enseñan qué teclas son.
    void RefreshArrows(bool[] touched)
    {
        // Con engranajes se marca el anillo de más afuera de los que arrastra la palanca:
        // las flechas quedan fuera del dibujo en vez de encima de él.
        int ring = -1;
        for (int i = 0; i < RingCount; i++) if (touched[i]) ring = i;

        bool visible = ring >= 0 && !resolved;
        if (arrowLeft != null) arrowLeft.gameObject.SetActive(visible);
        if (arrowRight != null) arrowRight.gameObject.SetActive(visible);
        if (!visible) return;

        float radius = outerRadius * (ring + 1) / activeRings;
        arrowLeft.anchoredPosition = new Vector2(-(radius + 15f), 0f);
        arrowRight.anchoredPosition = new Vector2(radius + 15f, 0f);

        Color color = RingColor(ring);
        foreach (var image in arrowImages)
            if (image != null) image.color = new Color(color.r, color.g, color.b, 0.9f);
    }

    void RefreshFooter()
    {
        footer.text = "PUNTERO SOBRE EL ANILLO  o  ↑ ↓      RUEDA  o  ← →   GIRAR      ESC  SALIR";
    }

    // El reloj va en su propio hueco y grande, no escondido al final del pie. Es la
    // única forma de perder el intento: tiene que verse sin buscarlo.
    void RefreshTimer()
    {
        if (timerText == null) return;

        if (timeLeft <= 0f) { timerText.text = ""; return; }

        int s = Mathf.CeilToInt(timeLeft);
        timerText.text = $"VENTANA  {s} s";

        // Ámbar en los últimos veinte, rojo en los últimos diez. El color avisa antes
        // de que haga falta leer el número.
        timerText.color = timeLeft <= 10f ? new Color(0.95f, 0.35f, 0.30f)
                        : timeLeft <= 20f ? HoloUI.Accent
                        : HoloUI.TextDim;
    }

    // ── Filas de control ────────────────────────────────────────────────────────

    // Cada control es una fila con las muestras de color de los anillos que mueve. La
    // muestra es la que hace la conexión inmediata con el dibujo; el texto solo
    // acompaña.
    void BuildControlRows()
    {
        foreach (var go in rowObjects) if (go != null) { go.SetActive(false); Destroy(go); }
        rowObjects.Clear();
        rowHighlights.Clear();
        rowLabels.Clear();

        for (int i = 0; i < ControlCount; i++)
        {
            var rowGO = new GameObject("Control" + i);
            rowGO.transform.SetParent(controlList, false);
            rowObjects.Add(rowGO);

            var highlight = rowGO.AddComponent<Image>();
            highlight.color = Color.clear;
            highlight.raycastTarget = false;

            var rt = rowGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 40f);
            rt.anchoredPosition = new Vector2(0f, -i * 46f);
            rowHighlights.Add(highlight);

            // Muestras de color: una por anillo que arrastra el control.
            var affected = new List<int>();
            if (geared)
            {
                var coupling = couplings[i];
                for (int c = 0; c < coupling.Length; c += 2) affected.Add(coupling[c]);
            }
            else affected.Add(i);

            for (int s = 0; s < affected.Count; s++)
            {
                var swatchGO = new GameObject("Muestra" + s);
                swatchGO.transform.SetParent(rowGO.transform, false);

                var swatch = swatchGO.AddComponent<Image>();
                swatch.color = RingColor(affected[s]);
                swatch.raycastTarget = false;

                var srt = swatchGO.GetComponent<RectTransform>();
                srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
                srt.pivot = new Vector2(0f, 0.5f);
                srt.sizeDelta = new Vector2(9f, 22f);
                srt.anchoredPosition = new Vector2(12f + s * 14f, 0f);
            }

            var label = HoloUI.CreateText(rowGO.transform, "Texto", 13, TextAnchor.MiddleLeft, HoloUI.TextDim);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = new Vector2(48f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            label.text = geared
                ? $"PALANCA {Roman(i + 1)}\n<color=#6F7BA0>{Describe(couplings[i])}</color>"
                : $"ANILLO {Roman(i + 1)}\n<color=#6F7BA0>{RingName(i)}</color>";

            rowLabels.Add(label);
        }
    }

    string Describe(int[] coupling)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < coupling.Length; i += 2)
        {
            if (i > 0) sb.Append("  +  ");
            sb.Append(RingName(coupling[i]));
            sb.Append(coupling[i + 1] > 0 ? " ↻" : " ↺");
        }
        return sb.ToString();
    }

    static string RingName(int ring) => ring switch
    {
        0 => "interior",
        1 => "medio",
        _ => "exterior",
    };

    static string Roman(int n) => n switch { 1 => "I", 2 => "II", _ => "III" };

    static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    // Los puntos van girados 45°: un cuadrado en diagonal se lee como estrella, y un
    // rombo pequeño aguanta mucho mejor la escala baja que un círculo dentado.
    Image AddDot(Transform parent, Vector2 position, float size, float alpha)
    {
        var go = new GameObject("Estrella");
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, alpha);
        image.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * size;
        rt.anchoredPosition = position;
        rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        return image;
    }

    // Un tramo de línea es un rectángulo fino rotado. No hace falta malla propia: la
    // UI de Unity no dibuja líneas, pero sí rectángulos girados.
    Image AddSegment(Transform parent, Vector2 from, Vector2 to, float thickness, float alpha)
    {
        var go = new GameObject("Trazo");
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, alpha);
        image.raycastTarget = false;

        Vector2 delta = to - from;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(delta.magnitude, thickness);
        rt.anchoredPosition = (from + to) * 0.5f;
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        return image;
    }

    // ── Construcción de la interfaz ─────────────────────────────────────────────

    void BuildUI()
    {
        var canvasGO = new GameObject("CanvasArmilar");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        canvasGO.AddComponent<CanvasScaler>();
        panelRoot = canvasGO;

        var panel = HoloUI.CreatePanel(canvasGO.transform, "PanelArmilar",
                                       new Vector2(920f, 640f), Vector2.zero,
                                       new Vector2(0.5f, 0.5f), HoloUI.FillDeep);

        var frame = new GameObject("Marco");
        frame.transform.SetParent(panel, false);
        var frameImage = frame.AddComponent<Image>();
        frameImage.sprite = HoloUI.PanelBorder;
        frameImage.type = Image.Type.Sliced;
        frameImage.color = HoloUI.Line;
        frameImage.raycastTarget = false;
        HoloUI.Stretch(frame.GetComponent<RectTransform>());

        header = HoloUI.CreateText(panel, "Cabecera", 21, TextAnchor.UpperLeft, HoloUI.Line);
        Anchor(header.rectTransform, new Vector2(30f, -24f), new Vector2(520f, 28f));

        status = HoloUI.CreateText(panel, "Estado", 13, TextAnchor.UpperRight, HoloUI.TextDim);
        AnchorRight(status.rectTransform, new Vector2(-30f, -26f), new Vector2(500f, 24f));

        // Qué hay que hacer, dicho con todas las letras. No lo decía en ningún sitio: la
        // cabecera daba el nombre del aparato, el estado el del cuerpo y el pie las
        // teclas, pero nada decía que había que hacer coincidir una cosa con la otra.
        // Para quien lo juega una sola vez y sin contexto, esa frase es la diferencia
        // entre entenderlo y abandonar.
        goal = HoloUI.CreateText(panel, "Objetivo", 13, TextAnchor.UpperLeft, HoloUI.Text);
        Anchor(goal.rectTransform, new Vector2(30f, -68f), new Vector2(560f, 20f));

        timerText = HoloUI.CreateText(panel, "Reloj", 16, TextAnchor.UpperRight, HoloUI.TextDim);
        AnchorRight(timerText.rectTransform, new Vector2(-30f, -66f), new Vector2(260f, 24f));

        var divider = HoloUI.CreateDivider(panel, 1f, HoloUI.LineDim);
        divider.rectTransform.offsetMin = new Vector2(24f, 0f);
        divider.rectTransform.offsetMax = new Vector2(-24f, 0f);
        divider.rectTransform.anchoredPosition = new Vector2(0f, -58f);

        BuildDial(panel);
        BuildSidebar(panel);

        footer = HoloUI.CreateText(panel, "Pie", 12, TextAnchor.LowerLeft, HoloUI.TextDim);
        footer.rectTransform.anchorMin = footer.rectTransform.anchorMax = new Vector2(0f, 0f);
        footer.rectTransform.pivot = new Vector2(0f, 0f);
        footer.rectTransform.anchoredPosition = new Vector2(30f, 20f);
        footer.rectTransform.sizeDelta = new Vector2(840f, 24f);
    }

    // El disco. Los tres anillos son contenedores centrados en el mismo punto, así que
    // girar el contenedor gira su parte de la figura sobre el eje del instrumento, que
    // es justo lo que hace un anillo de verdad.
    void BuildDial(RectTransform panel)
    {
        var dialGO = new GameObject("Disco");
        dialGO.transform.SetParent(panel, false);
        dial = dialGO.AddComponent<RectTransform>();
        dial.anchorMin = dial.anchorMax = dial.pivot = new Vector2(0.5f, 0.5f);
        dial.sizeDelta = Vector2.one * outerRadius * 2f;
        dial.anchoredPosition = new Vector2(-160f, -24f);

        // Las guías van debajo de los anillos, así que su contenedor se crea primero.
        // Se llenan en cada intento, no acá.
        guideRoot = new GameObject("Guias").AddComponent<RectTransform>();
        guideRoot.SetParent(dial, false);
        guideRoot.anchorMin = guideRoot.anchorMax = guideRoot.pivot = new Vector2(0.5f, 0.5f);
        guideRoot.sizeDelta = Vector2.zero;

        for (int i = 0; i < RingCount; i++)
        {
            var ringGO = new GameObject("Anillo" + i);
            ringGO.transform.SetParent(dial, false);
            rings[i] = ringGO.AddComponent<RectTransform>();
            rings[i].anchorMin = rings[i].anchorMax = rings[i].pivot = new Vector2(0.5f, 0.5f);
            rings[i].sizeDelta = Vector2.zero;
        }

        BuildIndexMark();
        arrowLeft = BuildArrow(false);
        arrowRight = BuildArrow(true);
    }

    // Una punta de flecha: dos trazos que se juntan en un vértice. Cuelga del disco y no
    // de un anillo, porque tiene que quedarse quieta mientras el anillo gira debajo.
    RectTransform BuildArrow(bool derecha)
    {
        var go = new GameObject(derecha ? "FlechaDerecha" : "FlechaIzquierda");
        go.transform.SetParent(dial, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;

        float s = derecha ? 1f : -1f;
        arrowImages.Add(AddSegment(rt, new Vector2(-4f * s, 8f), new Vector2(5f * s, 0f), 3f, 1f));
        arrowImages.Add(AddSegment(rt, new Vector2(5f * s, 0f), new Vector2(-4f * s, -8f), 3f, 1f));

        go.SetActive(false);
        return rt;
    }

    // Circunferencias marcando dónde termina un anillo y empieza el otro, cada una del
    // color de su anillo. Sin esto no se entiende por qué la línea se corta justo ahí.
    // No giran: son la referencia fija contra la que se mide el giro.
    //
    // Se rehacen en cada intento porque el radio depende de cuántos anillos participan.
    // Dibujadas siempre en tercios, con dos anillos activos quedaban donde la figura no
    // se corta, que es peor que no dibujarlas.
    void BuildRingGuides()
    {
        Clear(guideRoot);
        for (int i = 0; i < RingCount; i++) guides[i].Clear();

        for (int ring = 0; ring < activeRings; ring++)
        {
            float radius = outerRadius * (ring + 1) / activeRings;
            const int steps = 84;

            for (int i = 0; i < steps; i++)
            {
                float a0 = i / (float)steps * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)steps * Mathf.PI * 2f;
                var p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                var p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;

                var seg = AddSegment(guideRoot, p0, p1, 1.5f, 0.14f);
                seg.color = new Color(RingColor(ring).r, RingColor(ring).g, RingColor(ring).b, 0.14f);
                guides[ring].Add(seg);
            }
        }
    }

    // Índice en las doce: la marca contra la que se alinea el cero de cada anillo.
    void BuildIndexMark()
    {
        var indexGO = new GameObject("Indice");
        indexGO.transform.SetParent(dial, false);
        var indexRT = indexGO.AddComponent<RectTransform>();
        indexRT.anchorMin = indexRT.anchorMax = indexRT.pivot = new Vector2(0.5f, 0.5f);
        indexRT.sizeDelta = Vector2.zero;

        var mark = AddSegment(indexRT, new Vector2(0f, outerRadius + 6f),
                              new Vector2(0f, outerRadius + 24f), 3f, 1f);
        mark.color = HoloUI.Accent;

        var cap = AddDot(indexRT, new Vector2(0f, outerRadius + 28f), 9f, 1f);
        cap.color = HoloUI.Accent;
    }

    void BuildSidebar(RectTransform panel)
    {
        var listGO = new GameObject("Controles");
        listGO.transform.SetParent(panel, false);
        controlList = listGO.AddComponent<RectTransform>();
        controlList.anchorMin = new Vector2(1f, 1f);
        controlList.anchorMax = new Vector2(1f, 1f);
        controlList.pivot = new Vector2(1f, 1f);
        controlList.sizeDelta = new Vector2(300f, 200f);
        controlList.anchoredPosition = new Vector2(-30f, -84f);

        var previewLabel = HoloUI.CreateText(panel, "RefTexto", 11, TextAnchor.UpperRight, HoloUI.TextDim);
        AnchorRight(previewLabel.rectTransform, new Vector2(-30f, -300f), new Vector2(280f, 20f));
        previewLabel.text = "CARTA DE REFERENCIA";

        // Recuadro propio para la referencia: se lee como una placa aparte y no como
        // una parte suelta del panel.
        var boxGO = new GameObject("CajaReferencia");
        boxGO.transform.SetParent(panel, false);
        var box = boxGO.AddComponent<Image>();
        box.sprite = HoloUI.PanelFill;
        box.type = Image.Type.Sliced;
        box.color = new Color(0.02f, 0.05f, 0.08f, 0.9f);
        box.raycastTarget = false;

        var boxRT = boxGO.GetComponent<RectTransform>();
        boxRT.anchorMin = boxRT.anchorMax = new Vector2(1f, 1f);
        boxRT.pivot = new Vector2(1f, 1f);
        boxRT.sizeDelta = new Vector2(170f, 170f);
        boxRT.anchoredPosition = new Vector2(-30f, -326f);

        var boxBorder = new GameObject("BordeReferencia");
        boxBorder.transform.SetParent(boxRT, false);
        var boxBorderImage = boxBorder.AddComponent<Image>();
        boxBorderImage.sprite = HoloUI.PanelBorder;
        boxBorderImage.type = Image.Type.Sliced;
        boxBorderImage.color = HoloUI.LineDim;
        boxBorderImage.raycastTarget = false;
        HoloUI.Stretch(boxBorder.GetComponent<RectTransform>());

        preview = new GameObject("Referencia").AddComponent<RectTransform>();
        preview.SetParent(boxRT, false);
        preview.anchorMin = preview.anchorMax = preview.pivot = new Vector2(0.5f, 0.5f);
        preview.sizeDelta = Vector2.zero;
    }

    static void Anchor(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    static void AnchorRight(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    float Table(float[] table, float fallback)
        => table == null || table.Length == 0 ? fallback : table[Mathf.Clamp(attempt, 0, table.Length - 1)];

    int Table(int[] table, int fallback)
        => table == null || table.Length == 0 ? fallback : table[Mathf.Clamp(attempt, 0, table.Length - 1)];
}
