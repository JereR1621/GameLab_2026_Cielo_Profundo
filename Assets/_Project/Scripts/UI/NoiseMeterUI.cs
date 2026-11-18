using UnityEngine;
using UnityEngine.UI;

// Cuánto ruido estás haciendo, que es lo único que la araña percibe de ti.
//
// Hasta ahora el sigilo era completamente invisible: agacharse, correr y desplegar el
// radar cambiaban el radio de audición y no había forma de saberlo salvo que te
// cazaran. Un sistema de sigilo sin lectura no se juega, se adivina.
//
// La barra no es un número exacto a propósito. No dice "42 unidades", dice si estás
// callado o gritando, que es la decisión que tienes que tomar.
public class NoiseMeterUI : MonoBehaviour
{
    [SerializeField] float barWidth = 128f;

    [Tooltip("Se actualiza a saltos, como todo lo demás del juego. Una barra que se desliza suave se ve de otro juego.")]
    [SerializeField] float frameRate = 12f;

    [Header("Colores por nivel")]
    [SerializeField] Color quiet = new Color(0.42f, 0.84f, 0.92f);
    [SerializeField] Color loud = new Color(0.96f, 0.58f, 0.32f);
    [SerializeField] Color screaming = new Color(0.94f, 0.35f, 0.32f);

    RectTransform fill;
    Image fillImage;
    Text label;
    int lastFrame = -1;

    void Awake() => BuildUI();

    void Update()
    {
        var noise = NoiseEmitter.Instance;
        if (noise == null) return;

        int frame = Mathf.FloorToInt(Time.time * frameRate);
        if (frame == lastFrame) return;
        lastFrame = frame;

        float t = Mathf.Clamp01(noise.NoiseRadius / Mathf.Max(1f, noise.MaxNoiseRadius));

        fill.sizeDelta = new Vector2(barWidth * t, fill.sizeDelta.y);

        // Tres tramos y no un degradado continuo: lo que importa es que se lea de un
        // vistazo en qué franja estás, no el valor exacto.
        fillImage.color = t < 0.35f ? quiet : (t < 0.7f ? loud : screaming);
        label.color = fillImage.color;

        label.text = t < 0.35f ? "RUIDO  ·  BAJO"
                   : t < 0.7f ? "RUIDO  ·  AUDIBLE"
                              : "RUIDO  ·  TE OYE";
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("NoiseCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 14;
        canvasGO.AddComponent<CanvasScaler>();

        // Debajo de la barra de aguante, en la misma columna del resto del HUD.
        //
        // Las posiciones de la columna están repartidas para que nada se pise: acá metí
        // esta barra en -108 sin mirar que las bengalas ya estaban en -104, y se montaban
        // las dos con la etiqueta encima.
        var trackGO = new GameObject("Pista");
        trackGO.transform.SetParent(canvasGO.transform, false);
        var track = trackGO.AddComponent<Image>();
        track.color = new Color(0.05f, 0.09f, 0.12f, 0.75f);
        track.raycastTarget = false;

        var trackRT = trackGO.GetComponent<RectTransform>();
        trackRT.anchorMin = new Vector2(0f, 1f);
        trackRT.anchorMax = new Vector2(0f, 1f);
        trackRT.pivot = new Vector2(0f, 1f);
        trackRT.sizeDelta = new Vector2(barWidth + 6f, 12f);
        trackRT.anchoredPosition = new Vector2(20f, -112f);

        var fillGO = new GameObject("Relleno");
        fillGO.transform.SetParent(trackRT, false);
        fillImage = fillGO.AddComponent<Image>();
        fillImage.color = quiet;
        fillImage.raycastTarget = false;

        fill = fillGO.GetComponent<RectTransform>();
        fill.anchorMin = new Vector2(0f, 0.5f);
        fill.anchorMax = new Vector2(0f, 0.5f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.sizeDelta = new Vector2(0f, 7f);
        fill.anchoredPosition = new Vector2(3f, 0f);

        label = HoloUI.CreateText(canvasGO.transform, "Etiqueta", 11, TextAnchor.UpperLeft, quiet);
        var labelRT = label.rectTransform;
        labelRT.anchorMin = new Vector2(0f, 1f);
        labelRT.anchorMax = new Vector2(0f, 1f);
        labelRT.pivot = new Vector2(0f, 1f);
        // Al costado de su barra y no debajo: emparejarlas horizontalmente deja claro que la
        // etiqueta describe ESA barra y no la de arriba, y ahorra una fila de la columna.
        labelRT.sizeDelta = new Vector2(240f, 14f);
        labelRT.anchoredPosition = new Vector2(20f + barWidth + 16f, -111f);
        label.text = "RUIDO  ·  BAJO";
    }
}
