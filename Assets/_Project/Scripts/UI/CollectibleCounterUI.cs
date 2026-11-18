using UnityEngine;
using UnityEngine.UI;

public class CollectibleCounterUI : MonoBehaviour
{
    Text label;

    void Awake()
    {
        BuildUI();
    }

    bool subscribed;

    // La suscripción va en Start y no en OnEnable: Inventory vive en otro GameObject,
    // y Unity no garantiza que su Awake corra antes del OnEnable de éste. Start sí
    // corre después de todos los Awake de la escena.
    void Start()
    {
        if (Inventory.Instance == null)
        {
            Debug.LogWarning("CollectibleCounterUI no encontró un Inventory en la escena.");
            return;
        }

        Inventory.Instance.OnChanged += Refresh;
        subscribed = true;
        Refresh();
    }

    void OnDestroy()
    {
        if (subscribed && Inventory.Instance != null)
            Inventory.Instance.OnChanged -= Refresh;
    }

    void Refresh()
    {
        if (Inventory.Instance == null) return;
        label.text = "Fichas  " + Inventory.Instance.Collected.Count + " / " + Inventory.Instance.Total;
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("CounterCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();

        var textGO = new GameObject("CounterText");
        textGO.transform.SetParent(canvasGO.transform, false);
        label = textGO.AddComponent<Text>();
        label.font = HoloUI.Font;
        label.fontSize = 18;
        label.color = new Color(0.918f, 0.949f, 1f);
        label.alignment = TextAnchor.UpperLeft;
        var rt = textGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(220f, 30f);
        rt.anchoredPosition = new Vector2(20f, -20f);
    }
}
