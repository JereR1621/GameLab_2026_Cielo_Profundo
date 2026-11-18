using UnityEngine;
using UnityEngine.UI;

// Lenguaje visual compartido de la interfaz: paneles holográficos con las esquinas
// cortadas en ángulo, borde fino encendido sobre relleno oscuro, y líneas de barrido.
// Todo generado por código, sin un solo asset de imagen.
public static class HoloUI
{
    public static readonly Color Line = new Color(0.42f, 0.84f, 0.92f, 0.95f);
    public static readonly Color LineDim = new Color(0.42f, 0.84f, 0.92f, 0.28f);
    public static readonly Color Fill = new Color(0.02f, 0.08f, 0.12f, 0.80f);
    public static readonly Color FillDeep = new Color(0.01f, 0.04f, 0.07f, 0.92f);
    public static readonly Color Text = new Color(0.80f, 0.92f, 0.97f);
    public static readonly Color TextDim = new Color(0.45f, 0.62f, 0.70f);
    public static readonly Color Accent = new Color(0.96f, 0.58f, 0.32f);

    const int Size = 64;
    const int Cut = 16;

    static Sprite panelFill;
    static Sprite panelBorder;
    static Texture2D scanlines;
    static Font font;

    // Fuente monoespaciada del sistema. La de reserva de Unity es Arial, que es lo que
    // hace que todo se lea como un formulario y no como un instrumento: el ancho fijo
    // alinea las columnas de números solo y da la lectura de terminal que pide el juego.
    //
    // Se prueban varias porque no todas están en todos los sistemas. Si no hay ninguna
    // se cae a la de Unity, que es fea pero existe siempre.
    static readonly string[] Candidatas =
    {
        "Consolas", "Lucida Console", "DejaVu Sans Mono", "Liberation Mono",
        "Menlo", "Courier New",
    };

    public static Font Font => font != null ? font : (font = BuscarFuente());

    static Font BuscarFuente()
    {
        var instaladas = Font.GetOSInstalledFontNames();

        foreach (var nombre in Candidatas)
        {
            foreach (var instalada in instaladas)
            {
                if (!string.Equals(instalada, nombre, System.StringComparison.OrdinalIgnoreCase)) continue;

                var creada = Font.CreateDynamicFontFromOSFont(nombre, 32);
                if (creada != null) return creada;
            }
        }

        // Arial antes que la fuente interna de Unity, y este orden importa en el juego
        // compilado. Resources.GetBuiltinResource devuelve NULL en un build cuando nada
        // referencia esa fuente, y un Text sin fuente no dibuja nada: el cuadro existe,
        // ocupa su sitio y se ve vacío. Es lo que dejó sin texto la carta de los objetos
        // y el contador de fichas, mientras los menús seguían bien porque estos sí
        // pasaban por la fuente del sistema.
        var arial = Font.CreateDynamicFontFromOSFont("Arial", 32);
        if (arial != null) return arial;

        Debug.LogWarning("HoloUI: no encontré ninguna fuente del sistema; se usa la de Unity.");
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    // Los títulos van con las letras separadas. Es lo que convierte un texto escrito en
    // un rótulo grabado: lo mismo que hace cualquier placa de instrumental.
    public static string Espaciar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;

        var sb = new System.Text.StringBuilder(texto.Length * 2);
        for (int i = 0; i < texto.Length; i++)
        {
            if (i > 0) sb.Append(texto[i] == ' ' ? "  " : " ");
            sb.Append(texto[i]);
        }
        return sb.ToString();
    }

    public static Sprite PanelFill => panelFill != null ? panelFill : (panelFill = BuildPanel(false));
    public static Sprite PanelBorder => panelBorder != null ? panelBorder : (panelBorder = BuildPanel(true));

    // Panel completo: relleno, borde y barrido, listo para meterle contenido adentro.
    // Devuelve el RectTransform del área de contenido, ya con margen.
    public static RectTransform CreatePanel(Transform parent, string name, Vector2 size, Vector2 anchoredPosition,
                                            Vector2 anchor, Color? fillColor = null)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);

        var fill = root.AddComponent<Image>();
        fill.sprite = PanelFill;
        fill.type = Image.Type.Sliced;
        fill.color = fillColor ?? Fill;

        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;

        AddScanlines(rt, size.y);

        var borderGO = new GameObject("Borde");
        borderGO.transform.SetParent(rt, false);
        var border = borderGO.AddComponent<Image>();
        border.sprite = PanelBorder;
        border.type = Image.Type.Sliced;
        border.color = Line;
        border.raycastTarget = false;
        Stretch(borderGO.GetComponent<RectTransform>());

        return rt;
    }

    public static Text CreateText(Transform parent, string name, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = Font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    public static Image CreateDivider(Transform parent, float thickness, Color color)
    {
        var go = new GameObject("Division");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, thickness);
        return img;
    }

    public static void Stretch(RectTransform rt, float margin = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin);
        rt.offsetMax = new Vector2(-margin, -margin);
    }

    // Barrido horizontal. Va con RawImage porque necesita repetirse muchas veces a
    // lo alto y ajustar el uvRect es la forma directa de conseguirlo.
    public static void AddScanlines(RectTransform parent, float height)
    {
        if (scanlines == null) scanlines = BuildScanlineTexture();

        var go = new GameObject("Barrido");
        go.transform.SetParent(parent, false);
        var raw = go.AddComponent<RawImage>();
        raw.texture = scanlines;
        raw.color = new Color(0.42f, 0.84f, 0.92f, 0.09f);
        raw.raycastTarget = false;
        raw.uvRect = new Rect(0f, 0f, 1f, Mathf.Max(1f, height / 4f));
        Stretch(go.GetComponent<RectTransform>(), 2f);
    }

    static Texture2D BuildScanlineTexture()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };

        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                tex.SetPixel(x, y, y == 0 ? Color.white : Color.clear);

        tex.Apply();
        return tex;
    }

    // Rectángulo con las cuatro esquinas cortadas en diagonal. El sprite sale con
    // bordes de 9 secciones para que al estirarlo los cortes no se deformen.
    static Sprite BuildPanel(bool borderOnly)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        const float outline = 2f;
        var pixels = new Color[Size * Size];

        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float edge = EdgeDistance(x, y);
                bool inside = edge >= 0f;

                Color c;
                if (!inside) c = Color.clear;
                else if (borderOnly) c = edge <= outline ? Color.white : Color.clear;
                else c = Color.white;

                pixels[y * Size + x] = c;
            }

        tex.SetPixels(pixels);
        tex.Apply();

        var border = Vector4.one * (Cut + 3);
        return Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f),
                             100f, 0, SpriteMeshType.FullRect, border);
    }

    // Distancia al borde más cercano, contando también los cortes diagonales.
    // Negativa fuera de la figura.
    static float EdgeDistance(int x, int y)
    {
        float left = x;
        float right = Size - 1 - x;
        float bottom = y;
        float top = Size - 1 - y;

        float diag = 0.70710678f; // 1 / raíz de 2
        float c1 = (x + y - Cut) * diag;
        float c2 = ((Size - 1 - x) + y - Cut) * diag;
        float c3 = (x + (Size - 1 - y) - Cut) * diag;
        float c4 = ((Size - 1 - x) + (Size - 1 - y) - Cut) * diag;

        return Mathf.Min(Mathf.Min(left, right, bottom), Mathf.Min(top, c1, Mathf.Min(c2, c3, c4)));
    }

    // ── Puntero ─────────────────────────────────────────────────────────────────
    //
    // Los paneles se manejan por posición del cursor y no por EventSystem. No hay
    // EventSystem en ninguna escena, y agregarlo obligaría además a poner un
    // GraphicRaycaster por canvas y un Button por fila. Como las filas ya tienen su
    // rectángulo de resaltado, alcanza con preguntar cuál contiene el puntero.

    // Índice de la fila bajo el cursor, o -1. Se prueba contra el resaltado porque es
    // el que ocupa la fila entera; el texto no llega a los bordes.
    public static int RowAtPointer(System.Collections.Generic.List<Image> rowHighlights)
    {
        if (rowHighlights == null) return -1;

        Vector2 pointer = Input.mousePosition;
        for (int i = 0; i < rowHighlights.Count; i++)
        {
            if (rowHighlights[i] == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    rowHighlights[i].rectTransform, pointer, null))
                return i;
        }
        return -1;
    }

    // Si el cursor se movió desde la última consulta. Hace falta porque el resaltado
    // por posición pelearía con el teclado: con el puntero quieto encima de una fila,
    // moverse con las flechas volvería a saltar a esa fila cada cuadro.
    //
    // El estado lo guarda quien llama, no esto: son varios paneles y cada uno consulta
    // en su propio Update.
    public static bool PointerMovedSince(ref Vector3 last)
    {
        Vector3 now = Input.mousePosition;
        bool moved = (now - last).sqrMagnitude > 1f;
        last = now;
        return moved;
    }
}
