using UnityEngine;
using UnityEngine.UI;

// Informe de la misión asignada: el cuerpo al que vas a bajar. Muestra el comunicado
// de Paralax y la nota de campo del cuerpo actual de la ruta.
//
// Es el lore DE LA MISIÓN, no el del juego: acá no hay nada sobre la compañía en
// general, solo lo que corresponde al destino que tienes asignado ahora.
public class MissionBriefUI : HoloPanel
{
    Text title;
    Text subtitle;
    Text message;
    Text note;
    Text progress;

    void Awake()
    {
        BuildUI();
        panelRoot.SetActive(false);
    }

    protected override void OnOpened()
    {
        Refresh();
    }

    void Update()
    {
        if (!IsOpen || !InputReady) return;

        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) ||
            Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
        {
            UISound.Click();
            Close();
        }
    }

    void Refresh()
    {
        var body = RouteDatabase.Current;
        if (body == null)
        {
            title.text = "SIN ASIGNACIÓN";
            subtitle.text = "";
            message.text = "No hay ningún cuerpo asignado en la ruta.";
            note.text = "";
            progress.text = "";
            return;
        }

        title.text = body.nombre.ToUpperInvariant();
        subtitle.text = $"{body.clase}  ·  {body.tipo}  ·  tramo {body.tramo}".ToUpperInvariant();
        message.text = body.transmision;
        note.text = body.nota;
        progress.text = $"CUERPO {RouteDatabase.CurrentIndex + 1} DE {RouteDatabase.Bodies.Length}";
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("MissionCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        canvasGO.AddComponent<CanvasScaler>();

        var panel = HoloUI.CreatePanel(canvasGO.transform, "PanelMision",
                                       new Vector2(700f, 460f), Vector2.zero,
                                       new Vector2(0.5f, 0.5f), HoloUI.FillDeep);
        panelRoot = panel.gameObject;

        title = HoloUI.CreateText(panel, "Titulo", 24, TextAnchor.UpperLeft, HoloUI.Line);
        Anchor(title.rectTransform, new Vector2(28f, -22f), new Vector2(-56f, 30f));

        progress = HoloUI.CreateText(panel, "Progreso", 13, TextAnchor.UpperRight, HoloUI.TextDim);
        Anchor(progress.rectTransform, new Vector2(28f, -26f), new Vector2(-56f, 20f));

        subtitle = HoloUI.CreateText(panel, "Subtitulo", 13, TextAnchor.UpperLeft, HoloUI.TextDim);
        Anchor(subtitle.rectTransform, new Vector2(28f, -52f), new Vector2(-56f, 20f));

        var divider = HoloUI.CreateDivider(panel, 1f, HoloUI.LineDim);
        divider.rectTransform.offsetMin = new Vector2(22f, 0f);
        divider.rectTransform.offsetMax = new Vector2(-22f, 0f);
        divider.rectTransform.anchoredPosition = new Vector2(0f, -80f);

        message = HoloUI.CreateText(panel, "Mensaje", 14, TextAnchor.UpperLeft, HoloUI.Text);
        Anchor(message.rectTransform, new Vector2(28f, -94f), new Vector2(-56f, 140f));

        var divider2 = HoloUI.CreateDivider(panel, 1f, HoloUI.LineDim);
        divider2.rectTransform.offsetMin = new Vector2(22f, 0f);
        divider2.rectTransform.offsetMax = new Vector2(-22f, 0f);
        divider2.rectTransform.anchoredPosition = new Vector2(0f, -246f);

        // La nota va en ámbar: es la observación de campo, no la voz de la empresa.
        note = HoloUI.CreateText(panel, "Nota", 14, TextAnchor.UpperLeft, HoloUI.Accent);
        Anchor(note.rectTransform, new Vector2(28f, -260f), new Vector2(-56f, 140f));

        var footDivider = HoloUI.CreateDivider(panel, 1f, HoloUI.LineDim);
        footDivider.rectTransform.offsetMin = new Vector2(22f, 0f);
        footDivider.rectTransform.offsetMax = new Vector2(-22f, 0f);
        footDivider.rectTransform.anchoredPosition = new Vector2(0f, -414f);

        var footer = HoloUI.CreateText(panel, "Pie", 12, TextAnchor.UpperLeft, HoloUI.TextDim);
        Anchor(footer.rectTransform, new Vector2(28f, -426f), new Vector2(-56f, 20f));
        footer.text = "E / ESC o CLIC  CERRAR";
    }

    static void Anchor(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }
}
