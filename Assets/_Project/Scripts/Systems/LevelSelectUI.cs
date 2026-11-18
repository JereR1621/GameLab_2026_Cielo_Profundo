using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Consola de selección de destino: eliges un cuerpo de la ruta de Paralax y se
// carga la escena del planeta con ese cuerpo asignado. A propósito NO muestra la
// nota de campo de cada cuerpo — eso se descubre al bajar, no eligiendo desde acá.
public class LevelSelectUI : HoloPanel
{
    [SerializeField] string targetSceneName = "Planet_Test_Procedural";

    [Tooltip("Estudio con la esfera armilar previo al descenso. Sin esto, confirmar baja directo al planeta.")]
    [SerializeField] ArmillarySurveyUI survey;

    RectTransform listContent;
    Text detailTitle;
    Text detailInfo;
    Text emptyLabel;
    Text progreso;

    readonly List<Text> rows = new List<Text>();
    readonly List<Image> rowHighlights = new List<Image>();

    Vector3 lastPointer;
    int selected;

    void Awake()
    {
        BuildUI();
        panelRoot.SetActive(false);
    }

    protected override void OnOpened()
    {
        Rebuild();
    }

    void Update()
    {
        if (!IsOpen) return;

        // Se ignora el frame de apertura: la misma pulsación de E que abrió el panel
        // llegaría hasta acá y confirmaría el destino sin dejarte elegir.
        if (!InputReady) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UISound.Click();
            Close();
            return;
        }

        var bodies = RouteDatabase.Bodies;
        if (bodies.Length == 0) return;

        int previous = selected;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) selected++;
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) selected--;

        // Ratón. Acá el clic NO confirma de una: esta lista tiene detalle al costado, y
        // bajar a un cuerpo equivocado por un clic de más sería feo. El primer clic
        // elige, el segundo sobre lo ya elegido confirma.
        bool moved = HoloUI.PointerMovedSince(ref lastPointer);
        int over = HoloUI.RowAtPointer(rowHighlights);

        if (over >= 0 && moved && RouteDatabase.IsUnlocked(over)) selected = over;

        // La navegación se topa en el último asignado en vez de recorrer los cerrados: se
        // ven en la lista, pero no se pueden elegir.
        selected = Mathf.Clamp(selected, 0, Mathf.Min(bodies.Length, RouteDatabase.UnlockedCount) - 1);
        if (selected != previous) { UISound.Move(); RefreshSelection(); }

        // El ratón también respeta lo asignado: hacer clic en un cuerpo cerrado no hace
        // nada. Sin esto se podía saltar el bloqueo apuntando directo a la fila.
        if (over >= 0 && RouteDatabase.IsUnlocked(over) && Input.GetMouseButtonDown(0))
        {
            if (over == selected && over == previous) { UISound.Click(); Confirm(); return; }
            selected = over;
            UISound.Move();
            RefreshSelection();
            return;
        }

        if (Input.GetMouseButtonDown(1)) { UISound.Click(); Close(); return; }

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))
        {
            UISound.Click();
            Confirm();
        }
    }

    void Confirm()
    {
        // Nunca a un cuerpo sin asignar, ni siquiera si algo dejó la selección ahí.
        if (!RouteDatabase.IsUnlocked(selected)) return;

        bool changedBody = RouteDatabase.CurrentIndex != selected;
        RouteDatabase.CurrentIndex = selected;

        // Cambiar de destino borra los intentos de estudio del anterior: los tres
        // intentos son de ese cuerpo, no una cuota global.
        if (changedBody) Expedition.ResetSurvey();

        // Antes de bajar va el estudio con la esfera armilar. Si no está cableado se baja
        // directo, que es como funcionaba hasta ahora.
        if (survey != null)
        {
            Close();
            survey.Open();
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SceneManager.LoadScene(targetSceneName);
    }

    void Rebuild()
    {
        foreach (var row in rows) Destroy(row.transform.parent.gameObject);
        rows.Clear();
        rowHighlights.Clear();

        var bodies = RouteDatabase.Bodies;
        emptyLabel.gameObject.SetActive(bodies.Length == 0);
        if (bodies.Length == 0)
        {
            detailTitle.text = "";
            detailInfo.text = "";
            return;
        }

        for (int i = 0; i < bodies.Length; i++)
        {
            var rowGO = new GameObject("Fila" + i);
            rowGO.transform.SetParent(listContent, false);

            var highlight = rowGO.AddComponent<Image>();
            highlight.color = Color.clear;
            highlight.raycastTarget = false;

            var rt = rowGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 26f);
            rt.anchoredPosition = new Vector2(0f, -i * 28f);

            var label = HoloUI.CreateText(rowGO.transform, "Texto", 13, TextAnchor.MiddleLeft, HoloUI.TextDim);
            HoloUI.Stretch(label.rectTransform, 0f);
            label.rectTransform.offsetMin = new Vector2(10f, 0f);
            // Los cuerpos que Paralax todavía no te asignó se listan igual: ver que la ruta
            // sigue más allá es lo que le da forma al progreso. Lo que no se ve es qué son.
            bool abierto = RouteDatabase.IsUnlocked(i);
            label.text = abierto ? bodies[i].nombre : "— — —";

            rows.Add(label);
            rowHighlights.Add(highlight);
        }

        selected = Mathf.Clamp(selected, 0, Mathf.Min(rows.Count, RouteDatabase.UnlockedCount) - 1);

        if (progreso != null)
            progreso.text = $"RUTA  {RouteDatabase.UnlockedCount} / {bodies.Length}";

        RefreshSelection();
    }

    void RefreshSelection()
    {
        var bodies = RouteDatabase.Bodies;
        if (bodies.Length == 0) return;

        for (int i = 0; i < rows.Count; i++)
        {
            bool active = i == selected;
            bool abierto = RouteDatabase.IsUnlocked(i);

            rows[i].color = !abierto ? new Color(0.26f, 0.32f, 0.38f)
                          : active ? HoloUI.Line
                                   : HoloUI.TextDim;

            rowHighlights[i].color = active && abierto
                ? new Color(0.42f, 0.84f, 0.92f, 0.14f)
                : Color.clear;
        }

        int idx = Mathf.Clamp(selected, 0, bodies.Length - 1);

        // Sin asignar: se dice en el idioma de la empresa, no con un candado. Paralax no
        // te bloquea contenido, te entrega la ruta por partes.
        if (!RouteDatabase.IsUnlocked(idx))
        {
            detailTitle.text = "SIN ASIGNAR";
            detailInfo.text = "Paralax no ha emitido la asignación de este cuerpo.\n\n" +
                              "Se entrega al cierre del anterior.";
            return;
        }

        var body = bodies[idx];
        detailTitle.text = body.nombre.ToUpperInvariant();
        detailInfo.text = $"{body.clase}\n{body.tipo}  ·  tramo {body.tramo}\n\nObjetos estimados: {body.itemsMin}–{body.itemsMax}";
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("LevelSelectCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        canvasGO.AddComponent<CanvasScaler>();

        var panel = HoloUI.CreatePanel(canvasGO.transform, "PanelDestino",
                                       new Vector2(760f, 440f), Vector2.zero,
                                       new Vector2(0.5f, 0.5f), HoloUI.FillDeep);
        panelRoot = panel.gameObject;

        var header = HoloUI.CreateText(panel, "Cabecera", 22, TextAnchor.UpperLeft, HoloUI.Line);
        Anchor(header.rectTransform, new Vector2(28f, -22f), new Vector2(-56f, 28f));
        header.text = "SELECCIÓN DE DESTINO";

        var divider = HoloUI.CreateDivider(panel, 1f, HoloUI.LineDim);
        divider.rectTransform.offsetMin = new Vector2(22f, 0f);
        divider.rectTransform.offsetMax = new Vector2(-22f, 0f);
        divider.rectTransform.anchoredPosition = new Vector2(0f, -56f);

        var listPanel = new GameObject("Lista");
        listPanel.transform.SetParent(panel, false);
        var listRT = listPanel.AddComponent<RectTransform>();
        listRT.anchorMin = new Vector2(0f, 0f);
        listRT.anchorMax = new Vector2(0f, 1f);
        listRT.pivot = new Vector2(0f, 1f);
        listRT.sizeDelta = new Vector2(260f, -90f);
        listRT.anchoredPosition = new Vector2(24f, -66f);
        listContent = listRT;

        emptyLabel = HoloUI.CreateText(listRT, "Vacio", 13, TextAnchor.UpperLeft, HoloUI.TextDim);
        HoloUI.Stretch(emptyLabel.rectTransform, 10f);
        emptyLabel.text = "Sin cuerpos en la ruta.";

        var vLine = new GameObject("SeparadorVertical");
        vLine.transform.SetParent(panel, false);
        var vImg = vLine.AddComponent<Image>();
        vImg.color = HoloUI.LineDim;
        vImg.raycastTarget = false;
        var vRT = vLine.GetComponent<RectTransform>();
        vRT.anchorMin = new Vector2(0f, 0f);
        vRT.anchorMax = new Vector2(0f, 1f);
        vRT.pivot = new Vector2(0.5f, 0.5f);
        vRT.sizeDelta = new Vector2(1f, -96f);
        vRT.anchoredPosition = new Vector2(298f, -26f);

        detailTitle = HoloUI.CreateText(panel, "DetalleTitulo", 19, TextAnchor.UpperLeft, HoloUI.Line);
        AnchorRight(detailTitle.rectTransform, new Vector2(-24f, -70f), new Vector2(400f, 26f));

        detailInfo = HoloUI.CreateText(panel, "DetalleInfo", 14, TextAnchor.UpperLeft, HoloUI.Text);
        AnchorRight(detailInfo.rectTransform, new Vector2(-24f, -102f), new Vector2(400f, 220f));

        var footDivider = HoloUI.CreateDivider(panel, 1f, HoloUI.LineDim);
        footDivider.rectTransform.offsetMin = new Vector2(22f, 0f);
        footDivider.rectTransform.offsetMax = new Vector2(-22f, 0f);
        footDivider.rectTransform.anchoredPosition = new Vector2(0f, -394f);

        var footer = HoloUI.CreateText(panel, "Pie", 12, TextAnchor.UpperLeft, HoloUI.TextDim);
        Anchor(footer.rectTransform, new Vector2(28f, -406f), new Vector2(-56f, 20f));
        footer.text = "↑ ↓ o RATÓN  ELEGIR      E o 2º CLIC  CONFIRMAR      ESC o CLIC DERECHO  CANCELAR";

        progreso = HoloUI.CreateText(panel, "Progreso", 12, TextAnchor.UpperRight, HoloUI.TextDim);
        AnchorRight(progreso.rectTransform, new Vector2(-28f, -26f), new Vector2(300f, 20f));
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
