using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Catálogo de fichas recuperadas. Dos columnas: la lista a la izquierda y el detalle
// de la seleccionada a la derecha, como una consola de a bordo.
public class InventoryUI : MonoBehaviour
{
    [SerializeField] PlayerController playerController;
    [SerializeField] PlayerCamera playerCamera;

    GameObject panelRoot;
    RectTransform listContent;
    Text detailTitle;
    Text detailSerial;
    Text detailBody;
    Text detailTier;
    Text detailSource;
    Image tierBand;
    Text emptyLabel;
    Text counter;

    readonly List<Text> rows = new List<Text>();
    readonly List<Image> rowHighlights = new List<Image>();
    readonly List<Image> rowBars = new List<Image>();

    CatalogEntry[] routeCatalog;
    Vector3 lastPointer;
    CursorLockMode previousLock;
    bool previousVisible;
    int selected;
    bool isOpen;
    bool subscribed;

    void Awake()
    {
        BuildUI();
        panelRoot.SetActive(false);
    }

    // En Start y no en OnEnable: Inventory está en otro GameObject y el orden de los
    // Awake entre objetos no está garantizado.
    void Start()
    {
        if (Inventory.Instance == null) return;
        Inventory.Instance.OnChanged += Rebuild;
        subscribed = true;
    }

    void OnDestroy()
    {
        if (subscribed && Inventory.Instance != null)
            Inventory.Instance.OnChanged -= Rebuild;

        // Si muere abierto —cambio de escena con el catálogo desplegado— hay que bajar
        // la bandera, o los terminales de la nave siguiente creerían que sigue arriba.
        if (isOpen) CatalogOpen = false;
    }

    void Update()
    {
        // C de Catálogo: E quedó para tomar objetos y Tab para desplegar el radar.
        if (Input.GetKeyDown(KeyCode.C)) Toggle();
        if (!isOpen || rows.Count == 0) return;

        // Ratón: el catálogo es solo de lectura, así que el clic elige la ficha y nada
        // más. Clic derecho cierra, igual que C.
        bool moved = HoloUI.PointerMovedSince(ref lastPointer);
        int over = HoloUI.RowAtPointer(rowHighlights);
        if (over >= 0 && (moved || Input.GetMouseButtonDown(0)) && over != selected)
        {
            selected = over;
            UISound.Move();
            RefreshSelection();
        }

        if (Input.GetMouseButtonDown(1)) { Toggle(); return; }

        int previous = selected;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) selected++;
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) selected--;

        selected = Mathf.Clamp(selected, 0, rows.Count - 1);
        if (selected != previous) { UISound.Move(); RefreshSelection(); }
    }

    // El catálogo no es un HoloPanel, así que los terminales de la nave no lo veían.
    // Con esto se enteran y no ofrecen "E para abrir" por encima de él.
    public static bool CatalogOpen { get; private set; }

    void Toggle()
    {
        // No se abre encima de un panel holográfico: quedarían los dos y cada uno
        // leyendo las mismas teclas.
        if (!isOpen && HoloPanel.AnyOpen) return;

        isOpen = !isOpen;
        CatalogOpen = isOpen;
        panelRoot.SetActive(isOpen);

        // Se devuelve el cursor como estaba en vez de forzarlo a bloqueado: en la nave
        // no hay cámara libre que lo vuelva a soltar, y cerrar el catálogo ahí dejaba
        // el cursor invisible para siempre.
        if (isOpen)
        {
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }

        if (playerController != null) playerController.enabled = !isOpen;
        if (playerCamera != null) playerCamera.enabled = !isOpen;
        if (isOpen) Rebuild();
    }

    // De dónde salen las fichas del catálogo. En el planeta, del inventario de la
    // jornada; en la nave, donde no hay inventario, del archivo que sobrevive entre
    // escenas. Así el mismo panel sirve en los dos lados sin duplicarlo.
    IReadOnlyList<CatalogEntry> Entries()
    {
        if (Inventory.Instance != null) return Inventory.Instance.Collected;
        return routeCatalog ??= CatalogDatabase.RecoveredEntries();
    }

    // ── Escala de tramos ────────────────────────────────────────────────────────
    //
    // El tramo hace de rareza, como los colores de arma de Borderlands, y por el mismo
    // motivo que allí funciona: un color por nivel deja leer la lista entera de un
    // vistazo, sin comparar nombres ni leer texto.
    //
    // Acá además significa algo. El tramo 1 es instrumental de trabajo; el 3 es la
    // lámpara que sigue encendida sin combustible y el casco cerrado desde dentro. La
    // escala de color no la invento: la tenía el lore y no se veía por ninguna parte.
    static Color TierColor(int tramo) => tramo switch
    {
        1 => new Color(0.72f, 0.78f, 0.84f),   // rutina, gris frío
        2 => new Color(0.98f, 0.66f, 0.28f),   // anomalía, ámbar
        _ => new Color(0.95f, 0.36f, 0.42f),   // crítico, rojo
    };

    static string TierName(int tramo) => tramo switch
    {
        1 => "RUTINA",
        2 => "ANOMALÍA",
        _ => "CRÍTICO",
    };

    static string Roman(int tramo) => tramo switch { 1 => "I", 2 => "II", _ => "III" };

    // De quién es la voz de la ficha. Distingue el informe de los que estuvieron antes
    // del papeleo de la empresa, que es la tensión de todo el catálogo.
    static string SourceName(string voice) =>
        voice == "paralax" ? "ARCHIVO DE PARALAX" : "REGISTRO DE CUADRILLA";

    void Rebuild()
    {
        foreach (var row in rows) Destroy(row.transform.parent.gameObject);
        rows.Clear();
        rowHighlights.Clear();
        rowBars.Clear();

        var collected = Entries();

        // En la superficie el contador es el de la jornada; en la nave, el del archivo
        // completo de la ruta. Son dos preguntas distintas y conviene no mezclarlas.
        counter.text = Inventory.Instance != null
            ? $"RECUPERADAS  {collected.Count} / {Inventory.Instance.Total}"
            : $"ARCHIVO  {collected.Count} / {CatalogDatabase.All.Length}";

        bool empty = collected == null || collected.Count == 0;
        emptyLabel.gameObject.SetActive(empty);
        if (empty)
        {
            detailTitle.text = "";
            detailSerial.text = "";
            detailBody.text = "";
            detailTier.text = "";
            detailSource.text = "";
            tierBand.color = Color.clear;
            return;
        }

        for (int i = 0; i < collected.Count; i++)
        {
            var entry = collected[i];
            var color = TierColor(entry.tramo);

            var rowGO = new GameObject("Fila" + i);
            rowGO.transform.SetParent(listContent, false);

            var highlight = rowGO.AddComponent<Image>();
            highlight.color = Color.clear;
            highlight.raycastTarget = false;

            var rt = rowGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 32f);
            rt.anchoredPosition = new Vector2(0f, -i * 34f);

            // La barra de tramo, pegada al canto izquierdo. Es el elemento que más
            // trabaja de todo el panel: aunque no leas un solo nombre, la columna de
            // colores ya te dice qué llevas y de dónde salió.
            var barGO = new GameObject("Tramo");
            barGO.transform.SetParent(rowGO.transform, false);
            var bar = barGO.AddComponent<Image>();
            bar.color = color;
            bar.raycastTarget = false;
            var barRT = barGO.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0f, 0f);
            barRT.anchorMax = new Vector2(0f, 1f);
            barRT.pivot = new Vector2(0f, 0.5f);
            barRT.sizeDelta = new Vector2(4f, -8f);
            barRT.anchoredPosition = new Vector2(2f, 0f);

            // Punto con el color propio del objeto. Va aparte del tramo a propósito: el
            // tramo dice "cuánto pesa", el punto dice "cuál es". Mezclar las dos cosas en
            // un solo color deja las dos ilegibles.
            var dotGO = new GameObject("Punto");
            dotGO.transform.SetParent(rowGO.transform, false);
            var dot = dotGO.AddComponent<Image>();
            dot.color = entry.accentColor;
            dot.raycastTarget = false;
            var dotRT = dotGO.GetComponent<RectTransform>();
            dotRT.anchorMin = dotRT.anchorMax = new Vector2(0f, 0.5f);
            dotRT.pivot = new Vector2(0f, 0.5f);
            dotRT.sizeDelta = new Vector2(7f, 7f);
            dotRT.anchoredPosition = new Vector2(14f, 0f);

            var label = HoloUI.CreateText(rowGO.transform, "Texto", 13, TextAnchor.MiddleLeft, color);
            HoloUI.Stretch(label.rectTransform, 0f);
            label.rectTransform.offsetMin = new Vector2(28f, 0f);
            label.rectTransform.offsetMax = new Vector2(-76f, 0f);
            label.text = entry.entryTitle;

            // El tipo de pieza a la derecha, en pequeño. Deja buscar por forma —"¿dónde
            // está la otra lente?"— sin leer los nombres completos.
            var kind = HoloUI.CreateText(rowGO.transform, "Tipo", 10, TextAnchor.MiddleRight, HoloUI.TextDim);
            HoloUI.Stretch(kind.rectTransform, 0f);
            kind.rectTransform.offsetMax = new Vector2(-8f, 0f);
            kind.text = entry.shape.ToString().ToUpperInvariant();

            rows.Add(label);
            rowHighlights.Add(highlight);
            rowBars.Add(bar);
        }

        selected = Mathf.Clamp(selected, 0, rows.Count - 1);
        RefreshSelection();
    }

    void RefreshSelection()
    {
        var collected = Entries();
        if (collected.Count == 0) return;

        for (int i = 0; i < rows.Count; i++)
        {
            bool active = i == selected;
            var color = TierColor(collected[i].tramo);

            // La fila no elegida no se apaga a gris: se apaga a SU color, más flojo. Así
            // la escala de tramos sigue leyéndose entera aunque solo una esté marcada.
            rows[i].color = active ? Color.Lerp(color, Color.white, 0.45f) : color * 0.62f;
            rowBars[i].color = active ? color : color * 0.55f;
            rowHighlights[i].color = active
                ? new Color(color.r, color.g, color.b, 0.13f)
                : Color.clear;
        }

        var entry = collected[Mathf.Clamp(selected, 0, collected.Count - 1)];
        var tier = TierColor(entry.tramo);

        detailTitle.text = entry.entryTitle.ToUpperInvariant();
        detailTitle.color = tier;
        detailSerial.text = string.IsNullOrEmpty(entry.serial) ? "" : "SERIE  " + entry.serial;
        detailBody.text = entry.description;

        detailTier.text = $"TRAMO {Roman(entry.tramo)}  ·  {TierName(entry.tramo)}";
        detailTier.color = tier;
        detailSource.text = SourceName(entry.voice);

        // La banda de color detrás del título es lo que remata la lectura: el detalle
        // pertenece visiblemente al mismo nivel que la fila que elegiste.
        tierBand.color = new Color(tier.r, tier.g, tier.b, 0.16f);
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("InventoryCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        canvasGO.AddComponent<CanvasScaler>();

        var panel = HoloUI.CreatePanel(canvasGO.transform, "PanelCatalogo",
                                       new Vector2(840f, 470f), Vector2.zero,
                                       new Vector2(0.5f, 0.5f), HoloUI.FillDeep);
        panelRoot = panel.gameObject;

        var header = HoloUI.CreateText(panel, "Cabecera", 22, TextAnchor.UpperLeft, HoloUI.Line);
        Anchor(header.rectTransform, new Vector2(28f, -22f), new Vector2(-56f, 28f));
        header.text = "CATÁLOGO";

        counter = HoloUI.CreateText(panel, "Contador", 13, TextAnchor.UpperRight, HoloUI.TextDim);
        Anchor(counter.rectTransform, new Vector2(28f, -26f), new Vector2(-56f, 20f));

        var divider = HoloUI.CreateDivider(panel, 1f, HoloUI.LineDim);
        divider.rectTransform.offsetMin = new Vector2(22f, 0f);
        divider.rectTransform.offsetMax = new Vector2(-22f, 0f);
        divider.rectTransform.anchoredPosition = new Vector2(0f, -56f);

        // Columna izquierda: la lista.
        var listPanel = new GameObject("Lista");
        listPanel.transform.SetParent(panel, false);
        var listRT = listPanel.AddComponent<RectTransform>();
        listRT.anchorMin = new Vector2(0f, 0f);
        listRT.anchorMax = new Vector2(0f, 1f);
        listRT.pivot = new Vector2(0f, 1f);
        listRT.sizeDelta = new Vector2(300f, -110f);
        listRT.anchoredPosition = new Vector2(24f, -66f);
        listContent = listRT;

        emptyLabel = HoloUI.CreateText(listRT, "Vacio", 13, TextAnchor.UpperLeft, HoloUI.TextDim);
        HoloUI.Stretch(emptyLabel.rectTransform, 10f);
        emptyLabel.text = "Sin fichas recuperadas.";

        // Separador vertical entre columnas.
        var vLine = new GameObject("SeparadorVertical");
        vLine.transform.SetParent(panel, false);
        var vImg = vLine.AddComponent<Image>();
        vImg.color = HoloUI.LineDim;
        vImg.raycastTarget = false;
        var vRT = vLine.GetComponent<RectTransform>();
        vRT.anchorMin = new Vector2(0f, 0f);
        vRT.anchorMax = new Vector2(0f, 1f);
        vRT.pivot = new Vector2(0.5f, 0.5f);
        vRT.sizeDelta = new Vector2(1f, -116f);
        vRT.anchoredPosition = new Vector2(338f, -26f);

        // Columna derecha: el detalle.
        // Banda del color del tramo detrás de la cabecera del detalle. Es lo que ata
        // visualmente la ficha abierta con la fila de la que salió.
        var bandGO = new GameObject("BandaTramo");
        bandGO.transform.SetParent(panel, false);
        tierBand = bandGO.AddComponent<Image>();
        tierBand.color = Color.clear;
        tierBand.raycastTarget = false;
        AnchorRight(bandGO.GetComponent<RectTransform>(), new Vector2(-18f, -64f), new Vector2(474f, 58f));

        detailTitle = HoloUI.CreateText(panel, "DetalleTitulo", 17, TextAnchor.UpperLeft, HoloUI.Line);
        AnchorRight(detailTitle.rectTransform, new Vector2(-24f, -70f), new Vector2(462f, 24f));

        // Las dos etiquetas comparten renglón: el tramo pegado a la izquierda y la
        // procedencia a la derecha. Las dos cajas miden LO MISMO y ocupan el ancho
        // entero; solo cambia hacia qué lado se alinea el texto.
        //
        // Antes tenían anchos distintos (230 y 462) y las dos ancladas a la derecha, así
        // que la caja corta empezaba dentro de la larga y los textos se montaban uno
        // encima del otro: se leía "RUTINAREGISTRO DE CUADRILLA".
        detailTier = HoloUI.CreateText(panel, "DetalleTramo", 11, TextAnchor.UpperLeft, HoloUI.TextDim);
        AnchorRight(detailTier.rectTransform, new Vector2(-24f, -94f), new Vector2(462f, 16f));

        detailSource = HoloUI.CreateText(panel, "DetalleOrigen", 11, TextAnchor.UpperRight, HoloUI.TextDim);
        AnchorRight(detailSource.rectTransform, new Vector2(-24f, -94f), new Vector2(462f, 16f));

        detailSerial = HoloUI.CreateText(panel, "DetalleSerie", 12, TextAnchor.UpperLeft, HoloUI.TextDim);
        AnchorRight(detailSerial.rectTransform, new Vector2(-24f, -116f), new Vector2(462f, 18f));

        detailBody = HoloUI.CreateText(panel, "DetalleTexto", 14, TextAnchor.UpperLeft, HoloUI.Text);

        // Sin truncar: el ajuste por defecto corta lo que no cabe en la caja, y las
        // leyendas largas perdían el final sin avisar de nada.
        detailBody.verticalOverflow = VerticalWrapMode.Overflow;
        AnchorRight(detailBody.rectTransform, new Vector2(-24f, -144f), new Vector2(462f, 270f));

        var footDivider = HoloUI.CreateDivider(panel, 1f, HoloUI.LineDim);
        footDivider.rectTransform.offsetMin = new Vector2(22f, 0f);
        footDivider.rectTransform.offsetMax = new Vector2(-22f, 0f);
        footDivider.rectTransform.anchoredPosition = new Vector2(0f, -424f);

        var footer = HoloUI.CreateText(panel, "Pie", 12, TextAnchor.UpperLeft, HoloUI.TextDim);
        Anchor(footer.rectTransform, new Vector2(28f, -436f), new Vector2(-56f, 20f));
        footer.text = "↑ ↓ o RATÓN  NAVEGAR          C o CLIC DERECHO  SALIR";
    }

    static void Anchor(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
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
}
