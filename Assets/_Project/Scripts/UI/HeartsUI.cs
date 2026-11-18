using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HeartsUI : MonoBehaviour
{
    [SerializeField] float heartSize = 26f;
    [SerializeField] float spacing = 6f;

    readonly List<Image> hearts = new List<Image>();
    RectTransform row;
    Sprite heartSprite;
    bool subscribed;

    static readonly Color Alive = new Color(0.94f, 0.35f, 0.38f);
    static readonly Color Lost = new Color(0.22f, 0.16f, 0.28f);

    void Awake()
    {
        heartSprite = MakeHeartSprite(32);
        BuildUI();
    }

    // En Start y no en OnEnable: PlayerHealth vive en otro objeto y el orden de los
    // Awake entre objetos no está garantizado.
    void Start()
    {
        if (PlayerHealth.Instance == null)
        {
            Debug.LogWarning("HeartsUI no encontró un PlayerHealth en la escena.");
            return;
        }

        BuildHearts(PlayerHealth.Instance.MaxHearts);
        PlayerHealth.Instance.OnChanged += Refresh;
        subscribed = true;
        Refresh();
    }

    void OnDestroy()
    {
        if (subscribed && PlayerHealth.Instance != null)
            PlayerHealth.Instance.OnChanged -= Refresh;
    }

    void Refresh()
    {
        if (PlayerHealth.Instance == null) return;
        int alive = PlayerHealth.Instance.Hearts;

        for (int i = 0; i < hearts.Count; i++)
            hearts[i].color = i < alive ? Alive : Lost;
    }

    void BuildHearts(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Heart" + i);
            go.transform.SetParent(row, false);
            var img = go.AddComponent<Image>();
            img.sprite = heartSprite;
            img.color = Alive;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(heartSize, heartSize);
            rt.anchoredPosition = new Vector2(i * (heartSize + spacing), 0f);

            hearts.Add(img);
        }
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("HeartsCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;
        canvasGO.AddComponent<CanvasScaler>();

        var rowGO = new GameObject("Row");
        rowGO.transform.SetParent(canvasGO.transform, false);
        row = rowGO.AddComponent<RectTransform>();
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 1f);
        row.sizeDelta = new Vector2(200f, heartSize);
        // Columna del HUD, de arriba abajo y sin solaparse: fichas -20, corazones -56,
        // aguante -92, ruido -112, bengalas -136. Cada uno deja hueco para su propio alto.
        row.anchoredPosition = new Vector2(20f, -56f);
    }

    // Corazón dibujado con la curva implícita clásica (x²+y²-1)³ - x²y³ ≤ 0,
    // con filtro de píxel duro para que combine con el resto de la interfaz.
    static Sprite MakeHeartSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color[size * size];
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float x = ((px + 0.5f) / size * 2f - 1f) * 1.18f;
                float y = ((py + 0.5f) / size * 2f - 1f) * 1.18f;

                float a = x * x + y * y - 1f;
                bool inside = a * a * a - x * x * y * y * y <= 0f;
                pixels[py * size + px] = inside ? Color.white : Color.clear;
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }
}
