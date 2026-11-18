using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menú principal. Mismo lenguaje visual que los paneles de la nave y mismos controles
// que todo el resto del juego: flechas o WS para moverse, E o Enter para confirmar,
// Esc para volver.
//
// Y también ratón. Lo había dejado solo con teclado por coherencia con los paneles de
// a bordo, pero un menú principal muestra el cursor: no aceptar el clic no se lee como
// una decisión, se lee como que está roto.
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] string title = "CIELO PROFUNDO";
    [SerializeField] string lobbySceneName = "Ship_Lobby";

    [Tooltip("Salida animada hacia la nave. Sin esto se carga de golpe, como antes.")]
    [SerializeField] MenuLaunchSequence launch;

    // Los toma MenuLaunchSequence para apagarlos en momentos distintos.
    public CanvasGroup ChromeGroup { get; private set; }
    public CanvasGroup BrandGroup { get; private set; }
    public CanvasGroup VeilGroup { get; private set; }

    enum Page { Root, Options, ConfirmNew }

    class Entry
    {
        public string label;
        public Func<string> value;      // columna derecha, null si es un botón simple
        public Action activate;         // E
        public Action<int> adjust;      // ← →
        public Func<bool> enabled;
    }

    RectTransform listRoot;
    Text titleText, subtitleText, hintText, footerText;

    readonly List<Entry> entries = new List<Entry>();
    readonly List<Text> rowLabels = new List<Text>();
    readonly List<Text> rowValues = new List<Text>();
    readonly List<Image> rowHighlights = new List<Image>();
    readonly List<GameObject> rowObjects = new List<GameObject>();

    Vector3 lastPointer;
    Page page = Page.Root;
    int selected;

    void Awake()
    {
        GameSettings.EnsureApplied();
        BuildUI();
        Show(Page.Root);
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (entries.Count == 0) return;

        int previous = selected;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) selected = Next(+1);
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) selected = Next(-1);

        // Ratón: aquí el clic activa directamente, porque estas filas son botones y no
        // una lista con detalle al costado. Es lo que espera cualquiera en un menú.
        bool moved = HoloUI.PointerMovedSince(ref lastPointer);
        int over = HoloUI.RowAtPointer(rowHighlights);
        bool overUsable = over >= 0 && IsEnabled(entries[over]);

        if (overUsable && moved) selected = over;
        if (selected != previous) { UISound.Move(); RefreshSelection(); }

        if (overUsable && Input.GetMouseButtonDown(0))
        {
            selected = over;
            RefreshSelection();
            UISound.Click();
            entries[over].activate?.Invoke();
            return;
        }

        // Botón derecho para volver, como Esc. Cuesta una línea y evita que alguien
        // que entró a opciones con el ratón tenga que buscar el teclado para salir.
        if (Input.GetMouseButtonDown(1) && page != Page.Root)
        {
            UISound.Click();
            Show(Page.Root);
            return;
        }

        var entry = entries[selected];

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Adjust(entry, +1);
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Adjust(entry, -1);

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
        {
            if (IsEnabled(entry)) { UISound.Click(); entry.activate?.Invoke(); }
        }

        if (Input.GetKeyDown(KeyCode.Escape) && page != Page.Root) { UISound.Click(); Show(Page.Root); }
    }

    void Adjust(Entry entry, int direction)
    {
        if (entry.adjust == null || !IsEnabled(entry)) return;
        entry.adjust(direction);
        UISound.Move();
        RefreshValues();
    }

    // Salta las opciones deshabilitadas al moverse, para que no haya que pasar por
    // encima de "continuar ruta" cuando todavía no hay ruta que continuar.
    int Next(int direction)
    {
        int index = selected;
        for (int i = 0; i < entries.Count; i++)
        {
            index = (index + direction + entries.Count) % entries.Count;
            if (IsEnabled(entries[index])) return index;
        }
        return selected;
    }

    static bool IsEnabled(Entry entry) => entry.enabled == null || entry.enabled();

    // ── Páginas ─────────────────────────────────────────────────────────────────

    void Show(Page next)
    {
        page = next;
        entries.Clear();

        switch (page)
        {
            case Page.Root: BuildRoot(); break;
            case Page.Options: BuildOptions(); break;
            case Page.ConfirmNew: BuildConfirm(); break;
        }

        selected = 0;
        if (!IsEnabled(entries[0])) selected = Next(+1);

        BuildRows();
        RefreshSelection();
    }

    void BuildRoot()
    {
        // Sin bajada. El nombre de la empresa se descubre jugando, en las transmisiones
        // y en los números de serie; puesto acá como eslogan sonaba a relleno.
        subtitleText.text = "";

        entries.Add(new Entry
        {
            label = Expedition.RouteCompleted ? "VOLVER A LA NAVE" : "CONTINUAR RUTA",
            value = () => Expedition.RouteCompleted
                ? $"ruta completa  ·  {Expedition.Recovered.Count} fichas"
                : HasProgress
                    ? $"cuerpo {RouteDatabase.CurrentIndex + 1} de {RouteDatabase.Bodies.Length}"
                    : "sin ruta iniciada",
            enabled = () => HasProgress,
            activate = GoToShip,
        });

        entries.Add(new Entry
        {
            label = "NUEVA RUTA",
            // Solo pregunta si hay algo que perder. Sin progreso arranca directo.
            activate = () => { if (HasProgress) Show(Page.ConfirmNew); else StartFresh(); },
        });

        entries.Add(new Entry { label = "OPCIONES", activate = () => Show(Page.Options) });
        entries.Add(new Entry { label = "SALIR", activate = Quit });

        hintText.text = "";
    }

    void BuildOptions()
    {
        subtitleText.text = "OPCIONES";

        entries.Add(new Entry
        {
            label = "VOLUMEN GENERAL",
            value = () => Bar(GameSettings.MasterVolume),
            adjust = d => GameSettings.MasterVolume += d * 0.05f,
        });

        entries.Add(new Entry
        {
            label = "VOLUMEN DE MÚSICA",
            value = () => Bar(GameSettings.MusicVolume),
            adjust = d => GameSettings.MusicVolume += d * 0.05f,
        });

        entries.Add(new Entry
        {
            label = "VOLUMEN DE EFECTOS",
            value = () => Bar(GameSettings.EffectsVolume),
            adjust = d => GameSettings.EffectsVolume += d * 0.05f,
        });

        entries.Add(new Entry
        {
            label = "SENSIBILIDAD DEL MOUSE",
            value = () => GameSettings.MouseSensitivity.ToString("0.0"),
            adjust = d => GameSettings.MouseSensitivity += d * 0.25f,
        });

        entries.Add(new Entry
        {
            label = "INVERTIR EJE VERTICAL",
            value = () => GameSettings.InvertY ? "SÍ" : "NO",
            adjust = _ => GameSettings.InvertY = !GameSettings.InvertY,
            activate = () => { GameSettings.InvertY = !GameSettings.InvertY; RefreshValues(); },
        });

        entries.Add(new Entry
        {
            label = "PANTALLA COMPLETA",
            value = () => GameSettings.Fullscreen ? "SÍ" : "NO",
            adjust = _ => GameSettings.Fullscreen = !GameSettings.Fullscreen,
            activate = () => { GameSettings.Fullscreen = !GameSettings.Fullscreen; RefreshValues(); },
        });

        entries.Add(new Entry
        {
            label = "RESTAURAR VALORES",
            activate = () => { GameSettings.ResetToDefaults(); RefreshValues(); },
        });

        entries.Add(new Entry { label = "VOLVER", activate = () => Show(Page.Root) });

        // Ya no hace falta avisar de nada: los tres volúmenes se oyen. El de efectos
        // mueve estos mismos clics mientras se recorre el menú, así que se prueba solo.
        hintText.text = "";
    }

    void BuildConfirm()
    {
        subtitleText.text = "NUEVA RUTA";

        entries.Add(new Entry { label = "SÍ, EMPEZAR DE NUEVO", activate = StartFresh });
        entries.Add(new Entry { label = "CANCELAR", activate = () => Show(Page.Root) });

        hintText.text = $"Se borra el catálogo recuperado ({Expedition.Recovered.Count} fichas)\n"
                      + "y la ruta vuelve al primer cuerpo. No se puede deshacer.";
    }

    void StartFresh()
    {
        Expedition.ResetAll();
        GoToShip();
    }

    // Única salida hacia la nave. Con la secuencia cableada se apaga la entrada del
    // menú y manda ella; sin ella se carga de golpe, como antes.
    void GoToShip()
    {
        if (launch == null)
        {
            SceneManager.LoadScene(lobbySceneName);
            return;
        }

        enabled = false;
        launch.Play();
    }

    static bool HasProgress => RouteDatabase.CurrentIndex > 0 || Expedition.Recovered.Count > 0;

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Barra de bloques: se lee de un vistazo mejor que un porcentaje, y encaja con la
    // tipografía de terminal del resto de la interfaz.
    static string Bar(float value01)
    {
        const int slots = 10;
        int filled = Mathf.RoundToInt(Mathf.Clamp01(value01) * slots);
        return new string('█', filled) + new string('·', slots - filled)
             + "  " + Mathf.RoundToInt(value01 * 100f) + "%";
    }

    // ── Filas ───────────────────────────────────────────────────────────────────

    void BuildRows()
    {
        foreach (var go in rowObjects) if (go != null) { go.SetActive(false); Destroy(go); }
        rowObjects.Clear();
        rowLabels.Clear();
        rowValues.Clear();
        rowHighlights.Clear();

        for (int i = 0; i < entries.Count; i++)
        {
            var rowGO = new GameObject("Fila" + i);
            rowGO.transform.SetParent(listRoot, false);
            rowObjects.Add(rowGO);

            var highlight = rowGO.AddComponent<Image>();
            highlight.color = Color.clear;
            highlight.raycastTarget = false;

            var rt = rowGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 34f);
            rt.anchoredPosition = new Vector2(0f, -i * 38f);
            rowHighlights.Add(highlight);

            var label = HoloUI.CreateText(rowGO.transform, "Texto", 16, TextAnchor.MiddleLeft, HoloUI.TextDim);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.55f, 1f);
            label.rectTransform.offsetMin = new Vector2(18f, 0f);
            label.rectTransform.offsetMax = Vector2.zero;
            label.text = entries[i].label;
            rowLabels.Add(label);

            var value = HoloUI.CreateText(rowGO.transform, "Valor", 14, TextAnchor.MiddleRight, HoloUI.TextDim);
            value.rectTransform.anchorMin = new Vector2(0.55f, 0f);
            value.rectTransform.anchorMax = new Vector2(1f, 1f);
            value.rectTransform.offsetMin = Vector2.zero;
            value.rectTransform.offsetMax = new Vector2(-18f, 0f);
            rowValues.Add(value);
        }

        RefreshValues();
    }

    void RefreshValues()
    {
        for (int i = 0; i < entries.Count && i < rowValues.Count; i++)
            rowValues[i].text = entries[i].value != null ? entries[i].value() : "";
    }

    void RefreshSelection()
    {
        for (int i = 0; i < rowHighlights.Count; i++)
        {
            bool active = i == selected;
            bool usable = IsEnabled(entries[i]);

            rowHighlights[i].color = active
                ? new Color(0.42f, 0.84f, 0.92f, 0.16f)
                : Color.clear;

            // Deshabilitado se ve deshabilitado: si no, el jugador cree que la opción
            // está rota en vez de entender que todavía no aplica.
            Color color = !usable ? new Color(0.30f, 0.38f, 0.44f)
                        : active ? HoloUI.Line
                        : HoloUI.TextDim;

            rowLabels[i].text = (active && usable ? "▸  " : "   ") + entries[i].label;
            rowLabels[i].color = color;
            rowValues[i].color = !usable ? new Color(0.26f, 0.32f, 0.38f) : HoloUI.TextDim;
        }

        var entry = entries[selected];
        footerText.text = entry.adjust != null
            ? "↑ ↓ o RATÓN  ELEGIR      ← →  AJUSTAR      E o CLIC  CONFIRMAR" + (page != Page.Root ? "      ESC  VOLVER" : "")
            : "↑ ↓ o RATÓN  ELEGIR      E o CLIC  CONFIRMAR" + (page != Page.Root ? "      ESC  VOLVER" : "");
    }

    // ── Interfaz ────────────────────────────────────────────────────────────────

    void BuildUI()
    {
        var canvasGO = new GameObject("CanvasMenu");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Tres grupos separados porque la salida los apaga en momentos distintos: los
        // controles se van primero, el título se queda solo un rato con el fondo
        // despejado, y el velo se abre mientras la cámara avanza.
        VeilGroup = MakeGroup(canvasGO.transform, "Velo", 0);
        BrandGroup = MakeGroup(canvasGO.transform, "Marca", 1);
        ChromeGroup = MakeGroup(canvasGO.transform, "Controles", 2);

        // Velo oscuro sobre el fondo 3D: el cielo se sigue viendo pero el texto no
        // pelea contra él.
        var veilGO = new GameObject("Oscurecedor");
        veilGO.transform.SetParent(VeilGroup.transform, false);
        var veil = veilGO.AddComponent<Image>();
        veil.color = new Color(0.01f, 0.02f, 0.04f, 0.55f);
        veil.raycastTarget = false;
        HoloUI.Stretch(veilGO.GetComponent<RectTransform>());

        // Título arriba a la izquierda, no centrado: centrado queda a póster de menú
        // genérico, y a un costado se lee como rótulo de instrumento.
        titleText = HoloUI.CreateText(BrandGroup.transform, "Titulo", 68, TextAnchor.UpperLeft, HoloUI.Line);
        Place(titleText.rectTransform, new Vector2(140f, -170f), new Vector2(1100f, 90f));
        titleText.text = HoloUI.Espaciar(title);

        subtitleText = HoloUI.CreateText(BrandGroup.transform, "Subtitulo", 18, TextAnchor.UpperLeft, HoloUI.Accent);
        Place(subtitleText.rectTransform, new Vector2(146f, -262f), new Vector2(900f, 28f));

        var rule = new GameObject("Regla");
        rule.transform.SetParent(BrandGroup.transform, false);
        var ruleImage = rule.AddComponent<Image>();
        ruleImage.color = HoloUI.LineDim;
        ruleImage.raycastTarget = false;
        Place(rule.GetComponent<RectTransform>(), new Vector2(146f, -300f), new Vector2(520f, 1f));

        var listGO = new GameObject("Lista");
        listGO.transform.SetParent(ChromeGroup.transform, false);
        listRoot = listGO.AddComponent<RectTransform>();
        Place(listRoot, new Vector2(140f, -350f), new Vector2(700f, 400f));

        hintText = HoloUI.CreateText(ChromeGroup.transform, "Aviso", 13, TextAnchor.UpperLeft, HoloUI.TextDim);
        Place(hintText.rectTransform, new Vector2(146f, -790f), new Vector2(760f, 60f));

        footerText = HoloUI.CreateText(ChromeGroup.transform, "Pie", 13, TextAnchor.LowerLeft, HoloUI.TextDim);
        var footRT = footerText.rectTransform;
        footRT.anchorMin = footRT.anchorMax = new Vector2(0f, 0f);
        footRT.pivot = new Vector2(0f, 0f);
        footRT.sizeDelta = new Vector2(900f, 24f);
        footRT.anchoredPosition = new Vector2(146f, 60f);

        var version = HoloUI.CreateText(ChromeGroup.transform, "Version", 12, TextAnchor.LowerRight, new Color(0.30f, 0.40f, 0.46f));
        var verRT = version.rectTransform;
        verRT.anchorMin = verRT.anchorMax = new Vector2(1f, 0f);
        verRT.pivot = new Vector2(1f, 0f);
        verRT.sizeDelta = new Vector2(500f, 22f);
        verRT.anchoredPosition = new Vector2(-60f, 60f);
        version.text = $"BUILD {Application.version}  ·  JAM 2026";
    }

    static CanvasGroup MakeGroup(Transform parent, string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetSiblingIndex(order);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var group = go.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    static void Place(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }
}
