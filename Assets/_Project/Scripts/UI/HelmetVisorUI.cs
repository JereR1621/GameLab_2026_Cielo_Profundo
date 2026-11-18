using UnityEngine;
using UnityEngine.UI;

// Marco del casco en primera persona. Es lo más barato que se puede hacer para
// vender el traje: en cuanto entras a apuntar, la pantalla deja de ser una cámara
// y pasa a ser lo que ve alguien de adentro de un casco.
public class HelmetVisorUI : MonoBehaviour
{
    [SerializeField] float fadeSpeed = 7f;

    CanvasGroup group;
    RectTransform crosshair;
    float targetAlpha;

    void Awake()
    {
        BuildUI();
        group.alpha = 0f;
    }

    void Update()
    {
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.deltaTime * fadeSpeed);

        if (group.alpha > 0.01f)
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);
            crosshair.localScale = Vector3.one * pulse;
        }
    }

    public void SetVisible(bool visible) => targetAlpha = visible ? 1f : 0f;

    void BuildUI()
    {
        var canvasGO = new GameObject("VisorCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 18;
        canvasGO.AddComponent<CanvasScaler>();
        group = canvasGO.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // Cuatro bandas opacas en los bordes: dejan una ventana ovalada en el medio,
        // que es lo que se ve desde adentro de un casco.
        var mask = MakeMaskSprite(256);
        var frameGO = new GameObject("MarcoCasco");
        frameGO.transform.SetParent(canvasGO.transform, false);
        var frameImg = frameGO.AddComponent<Image>();
        frameImg.sprite = mask;
        frameImg.color = new Color(0.02f, 0.015f, 0.04f, 0.97f);
        var frameRT = frameGO.GetComponent<RectTransform>();
        frameRT.anchorMin = Vector2.zero;
        frameRT.anchorMax = Vector2.one;
        frameRT.offsetMin = Vector2.zero;
        frameRT.offsetMax = Vector2.zero;

        var crossGO = new GameObject("Mira");
        crossGO.transform.SetParent(canvasGO.transform, false);
        var crossImg = crossGO.AddComponent<Image>();
        crossImg.sprite = MakeRingSprite(48, 0.62f);
        crossImg.color = new Color(0.94f, 0.5f, 0.32f, 0.9f);
        crosshair = crossGO.GetComponent<RectTransform>();
        crosshair.anchorMin = crosshair.anchorMax = new Vector2(0.5f, 0.5f);
        crosshair.sizeDelta = new Vector2(26f, 26f);
        crosshair.anchoredPosition = Vector2.zero;
    }

    // Opaco en los bordes, transparente en el óvalo central.
    static Sprite MakeMaskSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;

                // Óvalo más ancho que alto, como la abertura de un casco.
                float d = Mathf.Sqrt(nx * nx / 1.05f + ny * ny / 0.72f);
                float alpha = Mathf.SmoothStep(0f, 1f, (d - 0.82f) / 0.24f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    static Sprite MakeRingSprite(int size, float innerRadius01)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outer = size / 2f;
        float inner = outer * innerRadius01;
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                pixels[y * size + x] = (d <= outer && d >= inner) ? Color.white : Color.clear;
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }
}
