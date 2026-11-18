using UnityEngine;
using UnityEngine.UI;

public class ItemCardUI : MonoBehaviour
{
    CanvasGroup group;
    Text titleText;
    Text loreText;
    Text hintText;

    float targetAlpha;

    void Awake()
    {
        BuildUI();
        group.alpha = 0f;
    }

    // Tiempo SIN escalar, y esto no es un detalle: la secuencia de recogida congela el
    // reloj del juego mientras la carta está abierta, para que no puedan matarte
    // mientras lees. Con Time.deltaTime la opacidad nunca subía de cero y la carta era
    // invisible — existía, tenía el texto puesto, y no se veía.
    void Update()
    {
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime * 4f);
    }

    public void Show(CatalogEntry entry)
    {
        // El número de serie va junto al nombre, sin comentario. Es la pista que
        // el jugador tiene que juntar solo: el mismo aparece en cuerpos distintos.
        titleText.text = string.IsNullOrEmpty(entry.serial)
            ? entry.entryTitle
            : $"{entry.entryTitle}    <color=#6F7BA0>{entry.serial}</color>";
        titleText.color = entry.accentColor;
        loreText.text = entry.description;
        targetAlpha = 1f;
    }

    public void Hide()
    {
        targetAlpha = 0f;
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("ItemCardCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvasGO.AddComponent<CanvasScaler>();
        group = canvasGO.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        var panelGO = new GameObject("Card");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.027f, 0.016f, 0.078f, 0.88f);
        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0f);
        panelRT.anchorMax = new Vector2(0.5f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.sizeDelta = new Vector2(620f, 150f);
        panelRT.anchoredPosition = new Vector2(0f, 60f);

        titleText = CreateText(panelRT, "Title", 24, TextAnchor.UpperCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-40f, 34f), new Vector2(0f, -14f));

        loreText = CreateText(panelRT, "Lore", 15, TextAnchor.UpperCenter,
            new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var loreRT = loreText.GetComponent<RectTransform>();
        loreRT.offsetMin = new Vector2(28f, 32f);
        loreRT.offsetMax = new Vector2(-28f, -52f);
        loreText.color = new Color(0.85f, 0.87f, 0.95f);

        hintText = CreateText(panelRT, "Hint", 13, TextAnchor.LowerCenter,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 24f), new Vector2(0f, 12f));
        hintText.text = "E  —  guardar en el catálogo";
        hintText.color = new Color(0.435f, 0.788f, 0.831f, 0.8f);
    }

    static Text CreateText(RectTransform parent, string name, int size, TextAnchor anchor,
                           Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = HoloUI.Font;
        text.fontSize = size;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.white;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, anchor == TextAnchor.LowerCenter ? 0f : 1f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPos;
        return text;
    }
}
