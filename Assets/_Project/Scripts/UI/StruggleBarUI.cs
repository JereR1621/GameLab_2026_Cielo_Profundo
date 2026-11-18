using UnityEngine;
using UnityEngine.UI;

public class StruggleBarUI : MonoBehaviour
{
    CanvasGroup group;
    RectTransform fill;
    Text prompt;
    float targetAlpha;
    float barWidth;

    void Awake()
    {
        BuildUI();
        group.alpha = 0f;
    }

    void Update()
    {
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.deltaTime * 8f);
    }

    public void Show()
    {
        targetAlpha = 1f;
        SetProgress(0f);
    }

    public void Hide()
    {
        targetAlpha = 0f;
    }

    public void SetProgress(float progress01)
    {
        float p = Mathf.Clamp01(progress01);
        fill.sizeDelta = new Vector2(barWidth * p, fill.sizeDelta.y);

        // De coral a cian a medida que te zafas: el coral es el color de lo que quema
        // y el cian el de lo seguro, así que el gradiente ya significa algo.
        fill.GetComponent<Image>().color = Color.Lerp(
            new Color(0.94f, 0.45f, 0.42f),
            new Color(0.435f, 0.788f, 0.831f), p);
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("StruggleCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        canvasGO.AddComponent<CanvasScaler>();
        group = canvasGO.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        barWidth = 420f;

        var trackGO = new GameObject("Track");
        trackGO.transform.SetParent(canvasGO.transform, false);
        var trackImg = trackGO.AddComponent<Image>();
        trackImg.color = new Color(0.027f, 0.016f, 0.078f, 0.9f);
        var trackRT = trackGO.GetComponent<RectTransform>();
        trackRT.anchorMin = new Vector2(0.5f, 0f);
        trackRT.anchorMax = new Vector2(0.5f, 0f);
        trackRT.pivot = new Vector2(0.5f, 0f);
        trackRT.sizeDelta = new Vector2(barWidth + 8f, 30f);
        trackRT.anchoredPosition = new Vector2(0f, 120f);

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(trackRT, false);
        fillGO.AddComponent<Image>();
        fill = fillGO.GetComponent<RectTransform>();
        fill.anchorMin = new Vector2(0f, 0.5f);
        fill.anchorMax = new Vector2(0f, 0.5f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.sizeDelta = new Vector2(0f, 22f);
        fill.anchoredPosition = new Vector2(4f, 0f);

        var promptGO = new GameObject("Prompt");
        promptGO.transform.SetParent(canvasGO.transform, false);
        prompt = promptGO.AddComponent<Text>();
        prompt.font = HoloUI.Font;
        prompt.text = "¡E!  ¡E!  ¡E!";
        prompt.fontSize = 26;
        prompt.alignment = TextAnchor.MiddleCenter;
        prompt.color = new Color(0.918f, 0.949f, 1f);
        var promptRT = promptGO.GetComponent<RectTransform>();
        promptRT.anchorMin = new Vector2(0.5f, 0f);
        promptRT.anchorMax = new Vector2(0.5f, 0f);
        promptRT.pivot = new Vector2(0.5f, 0f);
        promptRT.sizeDelta = new Vector2(420f, 40f);
        promptRT.anchoredPosition = new Vector2(0f, 158f);
    }
}
