using UnityEngine;
using UnityEngine.UI;

public class FlareAmmoUI : MonoBehaviour
{
    [SerializeField] FlareLauncher launcher;

    Text label;
    bool subscribed;

    void Awake()
    {
        BuildUI();
    }

    // En Start y no en OnEnable: el lanzador está en otro GameObject y el orden de
    // los Awake entre objetos no está garantizado.
    void Start()
    {
        if (launcher == null) return;
        launcher.OnChanged += Refresh;
        subscribed = true;
        Refresh();
    }

    void OnDestroy()
    {
        if (subscribed && launcher != null) launcher.OnChanged -= Refresh;
    }

    void Refresh()
    {
        label.text = "Bengalas  " + launcher.Flares + " / " + launcher.MaxFlares;
        label.color = launcher.Flares > 0
            ? new Color(0.94f, 0.55f, 0.35f)
            : new Color(0.45f, 0.40f, 0.48f);
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("AmmoCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 14;
        canvasGO.AddComponent<CanvasScaler>();

        var textGO = new GameObject("AmmoText");
        textGO.transform.SetParent(canvasGO.transform, false);
        label = textGO.AddComponent<Text>();
        label.font = HoloUI.Font;
        label.fontSize = 15;
        label.alignment = TextAnchor.UpperLeft;
        var rt = textGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(220f, 24f);
        rt.anchoredPosition = new Vector2(20f, -136f);
    }
}
