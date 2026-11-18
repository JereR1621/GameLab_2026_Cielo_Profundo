using UnityEngine;
using UnityEngine.UI;

// Cartel de interacción, compartido por todo lo que se pueda usar con E.
//
// Funciona por pedidos y no por mostrar/ocultar: cada interactuable pide el cartel
// cada frame mientras estés en rango, y acá se elige el más cercano. Con el modelo
// anterior, alejarte de un objeto ocultaba el cartel aunque siguieras al lado de
// otro, porque el segundo ya se consideraba "en rango" y no lo volvía a mostrar.
public class InteractionPromptUI : MonoBehaviour
{
    CanvasGroup group;
    Text label;

    string bestAction;
    float bestDistance;
    bool hasRequest;

    void Awake()
    {
        BuildUI();
        group.alpha = 0f;
    }

    public void Request(string action, float distance)
    {
        if (hasRequest && distance >= bestDistance) return;

        hasRequest = true;
        bestDistance = distance;
        bestAction = action;
    }

    // Los pedidos llegan durante Update; acá se resuelve el ganador y se limpia para
    // el frame siguiente.
    void LateUpdate()
    {
        if (hasRequest) label.text = "E    " + bestAction;

        // Sin escalar por el reloj del juego: la recogida lo congela, y con Time.deltaTime
        // este cartel se quedaría clavado en pantalla detrás de la carta del objeto en vez
        // de apagarse.
        group.alpha = Mathf.MoveTowards(group.alpha, hasRequest ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        hasRequest = false;
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("PromptCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGO.AddComponent<CanvasScaler>();
        group = canvasGO.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        var bgGO = new GameObject("PromptBg");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bg = bgGO.AddComponent<Image>();
        bg.sprite = HoloUI.PanelFill;
        bg.type = Image.Type.Sliced;
        bg.color = HoloUI.FillDeep;
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0.5f, 0f);
        bgRT.anchorMax = new Vector2(0.5f, 0f);
        bgRT.pivot = new Vector2(0.5f, 0f);
        bgRT.sizeDelta = new Vector2(360f, 42f);
        bgRT.anchoredPosition = new Vector2(0f, 90f);

        var borderGO = new GameObject("Borde");
        borderGO.transform.SetParent(bgRT, false);
        var border = borderGO.AddComponent<Image>();
        border.sprite = HoloUI.PanelBorder;
        border.type = Image.Type.Sliced;
        border.color = HoloUI.Line;
        border.raycastTarget = false;
        HoloUI.Stretch(borderGO.GetComponent<RectTransform>());

        label = HoloUI.CreateText(bgRT, "PromptText", 15, TextAnchor.MiddleCenter, HoloUI.Text);
        HoloUI.Stretch(label.rectTransform);
    }
}
