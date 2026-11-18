using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// El comunicado de Paralax al llegar a un cuerpo. Tono de recursos humanos, seco
// y alentador — y cada vez más desfasado de lo que está pasando en realidad.
public class TransmissionUI : MonoBehaviour
{
    [SerializeField] float holdSeconds = 12f;
    [SerializeField] float fadeSpeed = 1.6f;

    CanvasGroup group;
    Text title;
    Text subtitle;
    Text message;
    Text note;

    void Awake()
    {
        BuildUI();
        group.alpha = 0f;
    }

    void Start()
    {
        var session = PlanetSession.Instance;
        if (session == null || session.Body == null) return;

        var b = session.Body;
        title.text = b.nombre.ToUpperInvariant();
        subtitle.text = $"{b.clase}  ·  {b.tipo}  ·  tramo {b.tramo}".ToUpperInvariant();
        message.text = b.transmision;
        note.text = b.nota;

        StartCoroutine(PlayOnce());
    }

    IEnumerator PlayOnce()
    {
        while (group.alpha < 1f)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 1f, Time.deltaTime * fadeSpeed);
            yield return null;
        }

        yield return new WaitForSeconds(holdSeconds);

        while (group.alpha > 0f)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.deltaTime * fadeSpeed);
            yield return null;
        }
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("TransmissionCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25;
        canvasGO.AddComponent<CanvasScaler>();
        group = canvasGO.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        var panel = HoloUI.CreatePanel(canvasGO.transform, "PanelTransmision",
                                       new Vector2(620f, 330f), new Vector2(-30f, -30f),
                                       new Vector2(1f, 1f));

        // Barra de cabecera con su propia división, como los paneles de la referencia.
        title = HoloUI.CreateText(panel, "Titulo", 21, TextAnchor.UpperLeft, HoloUI.Line);
        Place(title.rectTransform, new Vector2(26f, -22f), new Vector2(-52f, 26f));

        subtitle = HoloUI.CreateText(panel, "Subtitulo", 12, TextAnchor.UpperLeft, HoloUI.TextDim);
        Place(subtitle.rectTransform, new Vector2(26f, -48f), new Vector2(-52f, 18f));

        var divider = HoloUI.CreateDivider(panel, 1f, HoloUI.LineDim);
        divider.rectTransform.offsetMin = new Vector2(20f, 0f);
        divider.rectTransform.offsetMax = new Vector2(-20f, 0f);
        divider.rectTransform.anchoredPosition = new Vector2(0f, -72f);

        message = HoloUI.CreateText(panel, "Mensaje", 14, TextAnchor.UpperLeft, HoloUI.Text);
        Place(message.rectTransform, new Vector2(26f, -84f), new Vector2(-52f, 118f));

        var divider2 = HoloUI.CreateDivider(panel, 1f, HoloUI.LineDim);
        divider2.rectTransform.offsetMin = new Vector2(20f, 0f);
        divider2.rectTransform.offsetMax = new Vector2(-20f, 0f);
        divider2.rectTransform.anchoredPosition = new Vector2(0f, -208f);

        // La nota va en ámbar: es la observación de campo, no la voz de la empresa.
        note = HoloUI.CreateText(panel, "Nota", 13, TextAnchor.UpperLeft, HoloUI.Accent);
        Place(note.rectTransform, new Vector2(26f, -220f), new Vector2(-52f, 96f));
    }

    static void Place(RectTransform rt, Vector2 anchoredPosition, Vector2 size)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(anchoredPosition.x, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;
    }
}
