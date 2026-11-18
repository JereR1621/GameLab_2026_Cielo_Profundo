using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pausa. Sirve igual en la nave y en la superficie.
//
// Hasta ahora ESC solo soltaba el cursor: una vez dentro de una partida no había forma
// de salir al menú ni de tocar el volumen sin matar el proceso. Para alguien que prueba
// el juego diez minutos —un jurado de jam, por ejemplo— quedarse encerrado es de las
// peores primeras impresiones que se pueden dar.
//
// Con el tiempo congelado, y eso importa en la superficie: sin congelar, abrir la pausa
// mientras una araña viene hacia ti la dejaría seguir caminando y podrías morir leyendo
// el menú.
public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] PlayerController playerController;
    [SerializeField] PlayerCamera playerCamera;
    [SerializeField] string menuSceneName = "Main_Menu";

    [Tooltip("Si el jugador está en la superficie. Cambia el aviso al salir: en la nave no se pierde nada, bajando sí.")]
    [SerializeField] bool onSurface;

    class Entry
    {
        public string label;
        public Func<string> value;
        public Action activate;
        public Action<int> adjust;
    }

    readonly List<Entry> entries = new List<Entry>();
    readonly List<Text> rowLabels = new List<Text>();
    readonly List<Text> rowValues = new List<Text>();
    readonly List<Image> rowHighlights = new List<Image>();

    GameObject root;
    Text warning;
    int selected;
    bool open;
    bool confirmingExit;
    Vector3 lastPointer;

    // Estado del cursor y del tiempo de antes de abrir, para devolverlo tal cual estaba.
    // Restaurar a un valor fijo rompería la nave, donde el cursor está suelto, o la
    // superficie, donde está capturado.
    CursorLockMode previousLock;
    bool previousCursorVisible;
    float previousTimeScale;

    // Cuadro en que se abrió o cerró. La misma pulsación de ESC que abre llegaba al
    // Update del mismo cuadro y la cerraba al instante. Es el mismo resguardo que llevan
    // los paneles de la nave, el escondite y las fichas de inicio.
    int toggleFrame = -1;

    public bool IsOpen => open;

    void Update()
    {
        if (!open)
        {
            if (Time.frameCount != toggleFrame && Input.GetKeyDown(KeyCode.Escape) && PuedeAbrir())
                Open();
            return;
        }

        if (Time.frameCount == toggleFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (confirmingExit) { UISound.Click(); MostrarRaiz(); }
            else Close();
            return;
        }

        Navegar();
    }

    // No se abre encima de otra cosa. Las fichas de inicio, el forcejeo con la araña y
    // la pantalla de muerte ya capturan la partida por su cuenta, y una pausa encima
    // dejaría dos pantallas peleándose por ESC y por el cursor.
    bool PuedeAbrir()
    {
        if (HoloPanel.AnyOpen) return false;
        if (EndingUI.Running) return false;
        if (SpiderCatchSequence.Instance != null && SpiderCatchSequence.Instance.IsPlaying) return false;

        // Tampoco encima de la carta de un objeto. Esa secuencia ya congela el reloj y
        // espera a que pulses E, así que con la pausa abierta encima las dos leerían la
        // misma tecla: confirmar una opción del menú cerraría además la carta. Y el reloj
        // acabaría restaurándose dos veces desde dos sitios distintos.
        var recogida = FindAnyObjectByType<ItemPickupSequence>();
        if (recogida != null && recogida.IsPlaying) return false;

        var ficha = FindAnyObjectByType<CreatureBriefUI>();
        if (ficha != null && ficha.enabled) return false;

        var controles = FindAnyObjectByType<ControlsBriefUI>();
        if (controles != null && controles.IsShowing) return false;

        var muerte = FindAnyObjectByType<GameOverUI>();
        if (muerte != null && muerte.IsShown) return false;

        return true;
    }

    // ── Abrir y cerrar ──────────────────────────────────────────────────────────

    void Open()
    {
        if (root == null) BuildUI();

        open = true;
        toggleFrame = Time.frameCount;
        root.SetActive(true);

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        previousLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerController != null) playerController.enabled = false;
        if (playerCamera != null) playerCamera.enabled = false;

        MostrarRaiz();
        UISound.Click();
    }

    void Close()
    {
        open = false;
        toggleFrame = Time.frameCount;
        root.SetActive(false);

        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousLock;
        Cursor.visible = previousCursorVisible;

        if (playerController != null) playerController.enabled = true;
        if (playerCamera != null) playerCamera.enabled = true;

        UISound.Click();
    }

    // El tiempo se devuelve también si el objeto muere con la pausa abierta. Sin esto,
    // salir al menú dejaría timeScale en cero y el menú entero quedaría congelado — con
    // el detalle de que la música seguiría sonando, porque el audio no usa timeScale.
    void OnDisable()
    {
        if (open) Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
    }

    // ── Páginas ─────────────────────────────────────────────────────────────────

    void MostrarRaiz()
    {
        confirmingExit = false;
        entries.Clear();

        entries.Add(new Entry { label = "CONTINUAR", activate = Close });

        entries.Add(new Entry
        {
            label = "VOLUMEN GENERAL",
            value = () => Porcentaje(GameSettings.MasterVolume),
            adjust = d => GameSettings.MasterVolume = Mathf.Clamp01(GameSettings.MasterVolume + d * 0.05f),
        });

        entries.Add(new Entry
        {
            label = "VOLUMEN DE MÚSICA",
            value = () => Porcentaje(GameSettings.MusicVolume),
            adjust = d => GameSettings.MusicVolume = Mathf.Clamp01(GameSettings.MusicVolume + d * 0.05f),
        });

        entries.Add(new Entry { label = "SALIR AL MENÚ", activate = MostrarConfirmacion });

        warning.text = "";
        Rebuild();
    }

    void MostrarConfirmacion()
    {
        confirmingExit = true;
        entries.Clear();

        entries.Add(new Entry { label = "SÍ, SALIR", activate = Salir });
        entries.Add(new Entry { label = "CANCELAR", activate = MostrarRaiz });

        // Se dice exactamente qué se pierde. En la superficie es mucho: las fichas se
        // registran al despegar, así que salir a mitad de un descenso tira la jornada
        // igual que morir. En la nave no se pierde nada y también hay que decirlo, para
        // que nadie se quede sin salir por miedo a perder el progreso.
        warning.text = onSurface
            ? "Las fichas de este descenso no se han transmitido.\nSalir ahora las pierde, igual que morir."
            : "La ruta y el catálogo quedan guardados.";

        Rebuild();
    }

    void Salir()
    {
        // El tiempo primero: si se carga el menú con timeScale en cero, el menú arranca
        // congelado y no responde a nada.
        Time.timeScale = 1f;
        open = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UISound.Click();
        SceneManager.LoadScene(menuSceneName);
    }

    static string Porcentaje(float v) => Mathf.RoundToInt(v * 100f) + "%";

    // ── Navegación ──────────────────────────────────────────────────────────────

    void Navegar()
    {
        int anterior = selected;

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) selected++;
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) selected--;
        selected = (selected + entries.Count) % entries.Count;

        // El ratón manda solo si se movió. Sin esta comprobación, un ratón quieto sobre
        // una fila cualquiera pisaría la selección del teclado en cada cuadro.
        if (HoloUI.PointerMovedSince(ref lastPointer))
        {
            // Las filas son SIEMPRE cuatro, pero la página de confirmación solo usa dos.
            // Sin comprobar el tope, poner el ratón sobre una fila sobrante devolvía un
            // índice que no existe en la lista de opciones y reventaba cada fotograma.
            // Sale en el log del juego compilado, no en el editor, porque ahí no había
            // llegado a pasar el ratón por esa zona con la confirmación abierta.
            int bajo = HoloUI.RowAtPointer(rowHighlights);
            if (bajo >= 0 && bajo < entries.Count) selected = bajo;
        }

        if (selected != anterior) UISound.Move();

        var entry = entries[selected];

        if (entry.adjust != null)
        {
            int paso = 0;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) paso = 1;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) paso = -1;

            if (paso != 0)
            {
                entry.adjust(paso);
                GameSettings.Apply();
                UISound.Move();
            }
        }

        bool confirmar = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                      || Input.GetKeyDown(KeyCode.E)
                      || (Input.GetMouseButtonDown(0) && HoloUI.RowAtPointer(rowHighlights) == selected);

        if (confirmar && entry.activate != null)
        {
            UISound.Click();
            entry.activate();
            return;
        }

        Refresh();
    }

    // ── Interfaz ────────────────────────────────────────────────────────────────

    const float RowHeight = 40f;
    const float PanelWidth = 520f;

    void BuildUI()
    {
        root = new GameObject("CanvasPausa");
        root.transform.SetParent(transform, false);
        root.SetActive(false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Por encima de todo lo demás, fichas de inicio incluidas: si algo llegara a
        // quedar abierto por debajo, la pausa tiene que verse igual.
        canvas.sortingOrder = 300;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var velo = new GameObject("Velo");
        velo.transform.SetParent(root.transform, false);
        var veloImg = velo.AddComponent<Image>();
        veloImg.color = new Color(0f, 0f, 0f, 0.72f);
        veloImg.raycastTarget = false;
        HoloUI.Stretch(velo.GetComponent<RectTransform>());

        // Anclado al centro de la pantalla: la pausa se abre encima de lo que sea, así
        // que no puede colgar de una esquina como los paneles de la nave.
        var panel = HoloUI.CreatePanel(root.transform, "PanelPausa",
                                       new Vector2(PanelWidth, 400f), Vector2.zero,
                                       new Vector2(0.5f, 0.5f));

        var titulo = HoloUI.CreateText(panel, "Titulo", 24, TextAnchor.UpperLeft, HoloUI.Line);
        Place(titulo.rectTransform, new Vector2(28f, -26f), new Vector2(400f, 34f));
        titulo.text = HoloUI.Espaciar("PAUSA");

        for (int i = 0; i < 4; i++)
        {
            var fila = new GameObject("Fila" + i);
            fila.transform.SetParent(panel, false);
            var brillo = fila.AddComponent<Image>();
            brillo.color = Color.clear;
            Place(fila.GetComponent<RectTransform>(),
                  new Vector2(18f, -80f - i * RowHeight), new Vector2(PanelWidth - 36f, RowHeight));
            rowHighlights.Add(brillo);

            var etiqueta = HoloUI.CreateText(fila.transform, "Etiqueta", 17, TextAnchor.MiddleLeft, HoloUI.Text);
            var er = etiqueta.rectTransform;
            er.anchorMin = new Vector2(0f, 0f); er.anchorMax = new Vector2(1f, 1f);
            er.offsetMin = new Vector2(14f, 0f); er.offsetMax = new Vector2(-150f, 0f);
            rowLabels.Add(etiqueta);

            var valor = HoloUI.CreateText(fila.transform, "Valor", 15, TextAnchor.MiddleRight, HoloUI.TextDim);
            var vr = valor.rectTransform;
            vr.anchorMin = new Vector2(0f, 0f); vr.anchorMax = new Vector2(1f, 1f);
            vr.offsetMin = new Vector2(0f, 0f); vr.offsetMax = new Vector2(-16f, 0f);
            rowValues.Add(valor);
        }

        warning = HoloUI.CreateText(panel, "Aviso", 13, TextAnchor.UpperLeft, HoloUI.Accent);
        Place(warning.rectTransform, new Vector2(32f, -252f), new Vector2(PanelWidth - 64f, 60f));

        var pie = HoloUI.CreateText(panel, "Pie", 12, TextAnchor.UpperLeft, HoloUI.TextDim);
        Place(pie.rectTransform, new Vector2(32f, -350f), new Vector2(PanelWidth - 64f, 24f));
        pie.text = "↑ ↓  ELEGIR      ← →  AJUSTAR      E o CLIC  CONFIRMAR      ESC  VOLVER";
    }

    void Rebuild()
    {
        selected = 0;

        for (int i = 0; i < rowHighlights.Count; i++)
        {
            bool usada = i < entries.Count;
            rowHighlights[i].gameObject.SetActive(usada);
        }

        Refresh();
    }

    void Refresh()
    {
        for (int i = 0; i < entries.Count && i < rowLabels.Count; i++)
        {
            bool activa = i == selected;
            rowLabels[i].text = (activa ? "▸  " : "   ") + entries[i].label;
            rowLabels[i].color = activa ? HoloUI.Line : HoloUI.Text;
            rowValues[i].text = entries[i].value != null ? entries[i].value() : "";
            rowHighlights[i].color = activa ? new Color(0.42f, 0.84f, 0.92f, 0.14f) : Color.clear;
        }
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }
}
