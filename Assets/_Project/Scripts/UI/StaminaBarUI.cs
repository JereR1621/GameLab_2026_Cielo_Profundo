using UnityEngine;
using UnityEngine.UI;

public class StaminaBarUI : MonoBehaviour
{
    [SerializeField] PlayerController playerController;
    [SerializeField] float barWidth = 150f;

    CanvasGroup group;
    RectTransform fill;
    Image fillImage;

    static readonly Color Normal = new Color(0.435f, 0.788f, 0.831f);
    static readonly Color Spent = new Color(0.94f, 0.45f, 0.42f);

    void Awake()
    {
        BuildUI();
    }

    void Update()
    {
        if (playerController == null) return;

        float value = playerController.Stamina01;

        // Se desvanece cuando está llena y no corrés: sin esto la barra ocupa
        // pantalla todo el tiempo para no decir nada.
        bool relevant = value < 0.999f || playerController.IsRunning;
        group.alpha = Mathf.MoveTowards(group.alpha, relevant ? 1f : 0f, Time.deltaTime * 3f);

        fill.sizeDelta = new Vector2(barWidth * value, fill.sizeDelta.y);
        fillImage.color = playerController.IsExhausted ? Spent : Normal;
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("StaminaCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 14;
        canvasGO.AddComponent<CanvasScaler>();
        group = canvasGO.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 0f;

        var trackGO = new GameObject("Track");
        trackGO.transform.SetParent(canvasGO.transform, false);
        var trackImg = trackGO.AddComponent<Image>();
        trackImg.color = new Color(0.027f, 0.016f, 0.078f, 0.85f);
        var trackRT = trackGO.GetComponent<RectTransform>();
        trackRT.anchorMin = new Vector2(0f, 1f);
        trackRT.anchorMax = new Vector2(0f, 1f);
        trackRT.pivot = new Vector2(0f, 1f);
        trackRT.sizeDelta = new Vector2(barWidth + 6f, 12f);
        trackRT.anchoredPosition = new Vector2(20f, -92f);

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(trackRT, false);
        fillImage = fillGO.AddComponent<Image>();
        fillImage.color = Normal;
        fill = fillGO.GetComponent<RectTransform>();
        fill.anchorMin = new Vector2(0f, 0.5f);
        fill.anchorMax = new Vector2(0f, 0.5f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.sizeDelta = new Vector2(barWidth, 7f);
        fill.anchoredPosition = new Vector2(3f, 0f);
    }
}
