using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RadarUI : MonoBehaviour
{
    [SerializeField] Transform player;
    [Tooltip("De dónde se lee hacia dónde estás mirando. Normalmente la cámara: el radar gira para que 'adelante tuyo' sea siempre 'arriba' del radar.")]
    [SerializeField] Transform facingReference;
    [SerializeField] float detectionRange = 55f;

    [Tooltip("El modulo de ascenso. Se marca siempre y nunca se descarta por distancia: perder el camino de vuelta no es un reto, es una caminata a ciegas.")]
    [SerializeField] Transform extraction;

    [SerializeField] Color extractionColor = new Color(0.99f, 0.62f, 0.26f);
    [SerializeField] float radarPixelRadius = 60f;
    [SerializeField] float sweepDegreesPerSecond = 110f;

    [Header("Despliegue: entra y sale con Tab")]
    [Tooltip("La entrada se mueve a saltos de 12 fps, igual que el personaje y el fuego.")]
    [SerializeField] float slideFrameRate = 12f;
    [SerializeField] float slideDuration = 0.4f;
    [SerializeField] float hiddenOffsetX = 210f;

    RectTransform console;
    RectTransform inset;
    RectTransform sweep;
    float sweepAngle;

    bool deployed;
    float slideT;
    float restingX;
    int lastSlideFrame = -1;

    public bool IsDeployed => deployed;

    readonly Dictionary<Collectible, RectTransform> blips = new Dictionary<Collectible, RectTransform>();
    readonly List<Collectible> toRemove = new List<Collectible>();
    RectTransform extractionBlip;

    void Awake()
    {
        BuildUI();
        restingX = console.anchoredPosition.x;
        console.anchoredPosition = new Vector2(restingX + hiddenOffsetX, console.anchoredPosition.y);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab)) deployed = !deployed;

        AnimateSlide();
        AnimateSweep();
        UpdateBlips();
    }

    void AnimateSlide()
    {
        slideT = Mathf.MoveTowards(slideT, deployed ? 1f : 0f, Time.deltaTime / slideDuration);

        // A saltos, no continuo: es lo que le da el aire de dibujo animado viejo.
        int frame = Mathf.FloorToInt(Time.time * slideFrameRate);
        if (frame == lastSlideFrame) return;
        lastSlideFrame = frame;

        // Al entrar se pasa de largo y vuelve; al salir se va derecho.
        float eased = deployed ? BackOut(slideT) : slideT;
        console.anchoredPosition = new Vector2(
            Mathf.Lerp(restingX + hiddenOffsetX, restingX, eased),
            console.anchoredPosition.y);
    }

    // Rebote corto al final del recorrido.
    static float BackOut(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    void AnimateSweep()
    {
        sweepAngle = (sweepAngle + sweepDegreesPerSecond * Time.deltaTime) % 360f;
        sweep.localRotation = Quaternion.Euler(0f, 0f, -sweepAngle);
    }

    void UpdateBlips()
    {
        if (player == null) return;

        var reference = facingReference != null ? facingReference : player;
        float yaw = reference.eulerAngles.y;

        UpdateExtractionBlip(yaw);

        toRemove.Clear();
        foreach (var kv in blips)
            if (kv.Key == null || !kv.Key.gameObject.activeInHierarchy)
                toRemove.Add(kv.Key);
        foreach (var dead in toRemove)
        {
            Destroy(blips[dead].gameObject);
            blips.Remove(dead);
        }

        foreach (var c in Collectible.All)
        {
            if (c == null || !c.gameObject.activeInHierarchy) continue;

            Vector3 offset = c.transform.position - player.position;
            float dist = new Vector2(offset.x, offset.z).magnitude;

            if (dist > detectionRange)
            {
                if (blips.TryGetValue(c, out var oldBlip))
                {
                    Destroy(oldBlip.gameObject);
                    blips.Remove(c);
                }
                continue;
            }

            if (!blips.TryGetValue(c, out var blip))
            {
                blip = CreateBlip();
                blips[c] = blip;
            }

            // Radar orientado a tu mirada: "adelante tuyo" siempre es "arriba" del radar,
            // así que el mundo gira alrededor tuyo en vez de que gire el radar entero.
            // Se usa Quaternion.Euler (la rotación real de Unity) en vez de trigonometría
            // a mano, para no chocar con la convención zurda del motor en el eje Y.
            Vector3 rotatedOffset = Quaternion.Euler(0f, -yaw, 0f) * offset;
            Vector2 relative = new Vector2(rotatedOffset.x, rotatedOffset.z);
            Vector2 blipPos = relative.sqrMagnitude > 0.0001f
                ? relative.normalized * (Mathf.Min(relative.magnitude, detectionRange) / detectionRange) * radarPixelRadius
                : Vector2.zero;
            blip.anchoredPosition = blipPos;

            float proximity01 = 1f - Mathf.Clamp01(dist / detectionRange);
            float blinkSpeed = Mathf.Lerp(1.2f, 6f, proximity01);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * blinkSpeed);

            var img = blip.GetComponent<Image>();
            var col = img.color;
            col.a = Mathf.Lerp(0.4f, 1f, pulse);
            img.color = col;
            blip.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.3f, proximity01);
        }
    }

    RectTransform CreateBlip()
    {
        var go = new GameObject("Blip");
        go.transform.SetParent(inset, false);
        var img = go.AddComponent<Image>();
        img.sprite = MakeCircleSprite(24, 0f);
        img.color = new Color(0.918f, 0.949f, 1f, 1f);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(8f, 8f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    // La marca del módulo de ascenso.
    //
    // Va aparte de las fichas por dos motivos. Uno: nunca se descarta por distancia. Las
    // fichas desaparecen del radar más allá del alcance, que está bien porque encontrarlas
    // es el juego; el módulo no, porque perder el camino de vuelta no es un reto, es una
    // caminata a ciegas. Cuando queda fuera de alcance se pega al borde y sigue marcando
    // la dirección.
    //
    // Y dos: es de otro color y más grande. En un radar lleno de puntos blancos iguales,
    // el único que dice "por aquí se sale" tiene que distinguirse sin pensarlo.
    void UpdateExtractionBlip(float yaw)
    {
        if (extraction == null) return;

        if (extractionBlip == null)
        {
            extractionBlip = CreateBlip();
            extractionBlip.name = "BlipModulo";
            extractionBlip.sizeDelta = new Vector2(13f, 13f);
        }

        Vector3 offset = extraction.position - player.position;
        Vector3 rotated = Quaternion.Euler(0f, -yaw, 0f) * offset;
        Vector2 relative = new Vector2(rotated.x, rotated.z);

        // Recortado al borde en vez de descartado: fuera de alcance deja de decir a qué
        // distancia está, pero sigue diciendo hacia dónde.
        Vector2 pos = relative.sqrMagnitude > 0.0001f
            ? relative.normalized * (Mathf.Min(relative.magnitude, detectionRange) / detectionRange) * radarPixelRadius
            : Vector2.zero;

        extractionBlip.anchoredPosition = pos;

        // Con el catálogo cerrado late, igual que la baliza del propio módulo. Las dos
        // cosas laten a la vez y se leen como una sola señal.
        bool cerrado = Inventory.Instance != null && Inventory.Instance.IsComplete;
        float onda = cerrado ? 0.5f + 0.5f * Mathf.Sin(Time.time * 3.4f) : 0f;

        var img = extractionBlip.GetComponent<Image>();
        img.color = cerrado
            ? Color.Lerp(extractionColor, Color.white, onda)
            : new Color(extractionColor.r, extractionColor.g, extractionColor.b, 0.55f);
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("RadarCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();

        // Consola: el bezel cuadrado y oscuro donde va incrustada la pantalla circular.
        var consoleGO = new GameObject("RadarConsole");
        consoleGO.transform.SetParent(canvasGO.transform, false);
        var consoleImg = consoleGO.AddComponent<Image>();
        consoleImg.color = new Color(0.043f, 0.024f, 0.125f, 0.9f);
        var consoleRT = consoleGO.GetComponent<RectTransform>();
        console = consoleRT;
        consoleRT.anchorMin = new Vector2(1f, 1f);
        consoleRT.anchorMax = new Vector2(1f, 1f);
        consoleRT.pivot = new Vector2(1f, 1f);
        consoleRT.sizeDelta = new Vector2(150f, 150f);
        consoleRT.anchoredPosition = new Vector2(-24f, -24f);

        var frameGO = new GameObject("Frame");
        frameGO.transform.SetParent(consoleGO.transform, false);
        var frameImg = frameGO.AddComponent<Image>();
        frameImg.sprite = MakeCircleSprite(96, 0.94f);
        frameImg.color = new Color(0.435f, 0.788f, 0.831f, 0.7f);
        var frameRT = frameGO.GetComponent<RectTransform>();
        frameRT.anchorMin = frameRT.anchorMax = new Vector2(0.5f, 0.5f);
        frameRT.sizeDelta = new Vector2(136f, 136f);
        frameRT.anchoredPosition = Vector2.zero;

        // Pantalla del radar: circular, no cuadrada.
        var insetGO = new GameObject("RadarScreen");
        insetGO.transform.SetParent(consoleGO.transform, false);
        var insetImg = insetGO.AddComponent<Image>();
        insetImg.sprite = MakeCircleSprite(96, 0f);
        insetImg.color = new Color(0.027f, 0.016f, 0.078f, 0.95f);
        inset = insetGO.GetComponent<RectTransform>();
        inset.anchorMin = inset.anchorMax = new Vector2(0.5f, 0.5f);
        inset.sizeDelta = new Vector2(128f, 128f);
        inset.anchoredPosition = Vector2.zero;

        // Anillos de distancia, para leer "qué tan lejos" de un vistazo.
        CreateRing(0.62f, 0.35f);
        CreateRing(1.0f, 0.35f);

        // Marcas cardinales fijas en la pantalla.
        CreateTick(0f);
        CreateTick(90f);
        CreateTick(180f);
        CreateTick(270f);

        // Barrido giratorio: la seña de que el instrumento está activo, siempre.
        var sweepGO = new GameObject("Sweep");
        sweepGO.transform.SetParent(inset, false);
        var sweepImg = sweepGO.AddComponent<Image>();
        sweepImg.sprite = MakeSweepSprite(64, 55f);
        sweepImg.color = new Color(0.435f, 0.788f, 0.831f, 0.9f);
        sweep = sweepGO.GetComponent<RectTransform>();
        sweep.anchorMin = sweep.anchorMax = new Vector2(0.5f, 0.5f);
        sweep.sizeDelta = new Vector2(128f, 128f);
        sweep.anchoredPosition = Vector2.zero;

        // Flecha fija apuntando "arriba": representa al jugador, siempre mirando hacia
        // adelante en el radar. Lo que gira alrededor de ella es el mundo, no al revés.
        var headingGO = new GameObject("PlayerHeading");
        headingGO.transform.SetParent(inset, false);
        var headingImg = headingGO.AddComponent<Image>();
        headingImg.color = new Color(0.964f, 0.502f, 0.373f, 0.95f);
        var headingRT = headingGO.GetComponent<RectTransform>();
        headingRT.sizeDelta = new Vector2(4f, 18f);
        headingRT.anchorMin = headingRT.anchorMax = new Vector2(0.5f, 0.5f);
        headingRT.pivot = new Vector2(0.5f, 0f);
        headingRT.anchoredPosition = Vector2.zero;

        var labelGO = new GameObject("RadarLabel");
        labelGO.transform.SetParent(consoleGO.transform, false);
        var label = labelGO.AddComponent<Text>();
        label.font = HoloUI.Font;
        label.text = "RADAR";
        label.fontSize = 11;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.435f, 0.788f, 0.831f, 0.8f);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 0f);
        labelRT.anchorMax = new Vector2(1f, 0f);
        labelRT.pivot = new Vector2(0.5f, 1f);
        labelRT.sizeDelta = new Vector2(0f, 16f);
        labelRT.anchoredPosition = new Vector2(0f, -2f);
    }

    void CreateRing(float scale01, float alpha)
    {
        var go = new GameObject("Ring");
        go.transform.SetParent(inset, false);
        var img = go.AddComponent<Image>();
        img.sprite = MakeCircleSprite(64, 0.9f);
        img.color = new Color(0.435f, 0.788f, 0.831f, alpha);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        float size = 128f * scale01;
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;
    }

    void CreateTick(float angleDeg)
    {
        var go = new GameObject("Tick");
        go.transform.SetParent(inset, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.435f, 0.788f, 0.831f, 0.6f);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(2f, 8f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        float radius = 60f;
        Vector3 dir = Quaternion.Euler(0f, 0f, -angleDeg) * Vector3.up;
        rt.anchoredPosition = new Vector2(dir.x, dir.y) * radius;
        rt.localRotation = Quaternion.Euler(0f, 0f, -angleDeg);
    }

    static Sprite MakeCircleSprite(int size, float innerRadius01)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerR = size / 2f;
        float innerR = outerR * innerRadius01;
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                pixels[y * size + x] = (d <= outerR && d >= innerR) ? Color.white : Color.clear;
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    static Sprite MakeSweepSprite(int size, float arcDegrees)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerR = size / 2f;
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                float d = p.magnitude;
                if (d > outerR) { pixels[y * size + x] = Color.clear; continue; }

                float angle = Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;

                float a = angle <= arcDegrees ? 1f - (angle / arcDegrees) : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }
}
