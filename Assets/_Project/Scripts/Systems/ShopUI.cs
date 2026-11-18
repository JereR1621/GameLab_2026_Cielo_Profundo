using UnityEngine;
using UnityEngine.UI;

// Tienda de suministros de Paralax. Los créditos salen de transmitir fichas al
// despegar, así que todo lo que hay acá lo pagaste juntando cosas allá abajo.
//
// Tres clases de cosa, y la diferencia importa más que los precios:
//
//   SUMINISTRO   se acumula y se gasta. Las bengalas.
//   EQUIPO       se compra una vez y queda para siempre. Los corazones.
//   PREPARACIÓN  vale para el PRÓXIMO descenso y se consume al aterrizar.
//
// Las preparaciones son las que evitan que la tienda rompa el juego. Una mejora
// permanente que te haga más silencioso vuelve trivial toda la segunda mitad de la
// ruta; la misma mejora por un solo descenso es una decisión que tomas mirando a qué
// cuerpo vas.
public class ShopUI : HoloPanel
{
    Vector3 lastPointer;

    enum Kind { Supply, Gear, Prep }

    class Offer
    {
        public string name;
        public string detail;
        public int price;
        public Kind kind;

        // Qué tienes ya de esto, para mostrarlo en la ficha.
        public System.Func<string> state;

        // Por qué no se puede comprar ahora mismo, o null si sí se puede.
        public System.Func<string> blocked;

        public System.Action buy;
    }

    static readonly Offer[] Catalog =
    {
        new Offer
        {
            name = "Bengala de señales",
            kind = Kind.Supply,
            price = 45,
            detail = "Aturde de cerca y llama a todo lo que haya\ndentro del radio. Ya no se reponen solas\nal bajar: lo que gastas, lo compras.",
            state = () => $"Llevas {Expedition.Flares}",
            blocked = () => Expedition.Flares >= 5 ? "LANZADOR LLENO" : null,
            buy = () => Expedition.Flares++,
        },
        new Offer
        {
            name = "Sello de traje",
            kind = Kind.Gear,
            price = 200,
            detail = "Un corazón más, para siempre. No cura: sube\nel máximo. Cada descenso empieza con el\ntraje entero.",
            state = () => $"Capacidad {Expedition.MaxHearts} de 5",
            blocked = () => Expedition.MaxHearts >= 5 ? "CAPACIDAD MÁXIMA" : null,
            buy = () => Expedition.MaxHearts++,
        },
        new Offer
        {
            name = "Suela adherente",
            kind = Kind.Prep,
            price = 70,
            detail = "Pisadas mucho más silenciosas durante el\npróximo descenso. No afecta al radar:\nencenderlo te sigue delatando igual.",
            state = () => Expedition.QuietStepReady ? "Preparada" : "Sin preparar",
            blocked = () => Expedition.QuietStepReady ? "YA PREPARADA" : null,
            buy = () => Expedition.QuietStepReady = true,
        },
        new Offer
        {
            name = "Ración de oxígeno",
            kind = Kind.Prep,
            price = 60,
            detail = "Más aguante durante el próximo descenso.\nCorres tramos más largos, no más rápido\nni más callado.",
            state = () => Expedition.StaminaPrepReady ? "Preparada" : "Sin preparar",
            blocked = () => Expedition.StaminaPrepReady ? "YA PREPARADA" : null,
            buy = () => Expedition.StaminaPrepReady = true,
        },
    };

    static string KindLabel(Kind kind) => kind switch
    {
        Kind.Supply => "SUMINISTRO",
        Kind.Gear => "EQUIPO",
        _ => "PREPARACIÓN",
    };

    RectTransform listContent;
    Text detailTitle;
    Text detailBody;
    Text detailPrice;
    Text credits;

    readonly System.Collections.Generic.List<Text> rows = new System.Collections.Generic.List<Text>();
    readonly System.Collections.Generic.List<Image> rowHighlights = new System.Collections.Generic.List<Image>();

    int selected;

    void Awake()
    {
        BuildUI();
        BuildRows();
        panelRoot.SetActive(false);
    }

    protected override void OnOpened()
    {
        RefreshSelection();
    }

    void Update()
    {
        if (!IsOpen || !InputReady) return;

        // Ratón: el puntero elige y el segundo clic sobre lo ya elegido compra. Igual que
        // en la mesa de destinos: gastar créditos por un clic de más sería feo.
        bool moved = HoloUI.PointerMovedSince(ref lastPointer);
        int over = HoloUI.RowAtPointer(rowHighlights);
        if (over >= 0 && moved && over != selected)
        {
            selected = over;
            UISound.Move();
            RefreshSelection();
        }

        if (over >= 0 && Input.GetMouseButtonDown(0))
        {
            if (over == selected) { Buy(); return; }
            selected = over;
            UISound.Move();
            RefreshSelection();
            return;
        }

        if (Input.GetMouseButtonDown(1)) { Close(); return; }

        // E compra, no cierra. Se sale con Esc o con el botón derecho: en una tienda, la
        // tecla de acción tiene que ser la de comprar.
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return)) { Buy(); return; }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UISound.Click();
            Close();
            return;
        }

        int previous = selected;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) selected++;
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) selected--;
        selected = Mathf.Clamp(selected, 0, Catalog.Length - 1);
        if (selected != previous) { UISound.Move(); RefreshSelection(); }
    }

    void Buy()
    {
        var offer = Catalog[Mathf.Clamp(selected, 0, Catalog.Length - 1)];

        // No se compra si está topado ni si no alcanza. El motivo ya se ve en la ficha,
        // así que acá solo hay que no dejar pasar.
        if (offer.blocked?.Invoke() != null) return;
        if (Expedition.Credits < offer.price) return;

        Expedition.Credits -= offer.price;
        offer.buy?.Invoke();

        UISound.Click();
        RefreshSelection();
    }

    void BuildRows()
    {
        for (int i = 0; i < Catalog.Length; i++)
        {
            var rowGO = new GameObject("Fila" + i);
            rowGO.transform.SetParent(listContent, false);

            var highlight = rowGO.AddComponent<Image>();
            highlight.color = Color.clear;
            highlight.raycastTarget = false;

            var rt = rowGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 28f);
            rt.anchoredPosition = new Vector2(0f, -i * 30f);

            var label = HoloUI.CreateText(rowGO.transform, "Texto", 13, TextAnchor.MiddleLeft, HoloUI.TextDim);
            HoloUI.Stretch(label.rectTransform, 0f);
            label.rectTransform.offsetMin = new Vector2(10f, 0f);
            label.text = Catalog[i].name;

            rows.Add(label);
            rowHighlights.Add(highlight);
        }
    }

    void RefreshSelection()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            bool active = i == selected;
            rows[i].color = active ? HoloUI.Line : HoloUI.TextDim;
            rowHighlights[i].color = active ? new Color(0.42f, 0.84f, 0.92f, 0.14f) : Color.clear;
        }

        var offer = Catalog[selected];
        detailTitle.text = offer.name.ToUpperInvariant();

        string motivo = offer.blocked?.Invoke();
        bool sinPlata = motivo == null && Expedition.Credits < offer.price;

        detailBody.text = $"{KindLabel(offer.kind)}\n\n{offer.detail}\n\n{offer.state?.Invoke()}";

        // Se dice POR QUÉ no se puede, no solo que no se puede. "No disponible" a secas
        // deja al jugador sin saber si le falta dinero o si ya lo tiene al máximo.
        detailPrice.text = motivo != null ? $"{offer.price} créditos     ·     {motivo}"
                         : sinPlata ? $"{offer.price} créditos     ·     CRÉDITOS INSUFICIENTES"
                         : $"{offer.price} créditos     ·     E PARA COMPRAR";

        detailPrice.color = motivo != null || sinPlata ? new Color(0.72f, 0.52f, 0.42f) : HoloUI.Line;

        if (credits != null) credits.text = $"CRÉDITOS  {Expedition.Credits}";
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("ShopCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        canvasGO.AddComponent<CanvasScaler>();

        var panel = HoloUI.CreatePanel(canvasGO.transform, "PanelTienda",
                                       new Vector2(780f, 420f), Vector2.zero,
                                       new Vector2(0.5f, 0.5f), HoloUI.FillDeep);
        panelRoot = panel.gameObject;

        var header = HoloUI.CreateText(panel, "Cabecera", 22, TextAnchor.UpperLeft, HoloUI.Line);
        Anchor(header.rectTransform, new Vector2(28f, -22f), new Vector2(-56f, 28f));
        header.text = "SUMINISTROS";

        credits = HoloUI.CreateText(panel, "Creditos", 13, TextAnchor.UpperRight, HoloUI.TextDim);
        Anchor(credits.rectTransform, new Vector2(28f, -26f), new Vector2(-56f, 20f));
        credits.text = $"CRÉDITOS  {Expedition.Credits}";

        var divider = HoloUI.CreateDivider(panel, 1f, HoloUI.LineDim);
        divider.rectTransform.offsetMin = new Vector2(22f, 0f);
        divider.rectTransform.offsetMax = new Vector2(-22f, 0f);
        divider.rectTransform.anchoredPosition = new Vector2(0f, -56f);

        var listGO = new GameObject("Lista");
        listGO.transform.SetParent(panel, false);
        listContent = listGO.AddComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0f, 0f);
        listContent.anchorMax = new Vector2(0f, 1f);
        listContent.pivot = new Vector2(0f, 1f);
        listContent.sizeDelta = new Vector2(280f, -90f);
        listContent.anchoredPosition = new Vector2(24f, -66f);

        var vLine = new GameObject("SeparadorVertical");
        vLine.transform.SetParent(panel, false);
        var vImg = vLine.AddComponent<Image>();
        vImg.color = HoloUI.LineDim;
        vImg.raycastTarget = false;
        var vRT = vLine.GetComponent<RectTransform>();
        vRT.anchorMin = new Vector2(0f, 0f);
        vRT.anchorMax = new Vector2(0f, 1f);
        vRT.pivot = new Vector2(0.5f, 0.5f);
        vRT.sizeDelta = new Vector2(1f, -96f);
        vRT.anchoredPosition = new Vector2(318f, -26f);

        detailTitle = HoloUI.CreateText(panel, "DetalleTitulo", 18, TextAnchor.UpperLeft, HoloUI.Line);
        AnchorRight(detailTitle.rectTransform, new Vector2(-24f, -70f), new Vector2(400f, 24f));

        detailBody = HoloUI.CreateText(panel, "DetalleTexto", 14, TextAnchor.UpperLeft, HoloUI.Text);
        AnchorRight(detailBody.rectTransform, new Vector2(-24f, -100f), new Vector2(400f, 180f));

        detailPrice = HoloUI.CreateText(panel, "DetallePrecio", 14, TextAnchor.UpperLeft, HoloUI.Accent);
        AnchorRight(detailPrice.rectTransform, new Vector2(-24f, -300f), new Vector2(400f, 24f));

        var footDivider = HoloUI.CreateDivider(panel, 1f, HoloUI.LineDim);
        footDivider.rectTransform.offsetMin = new Vector2(22f, 0f);
        footDivider.rectTransform.offsetMax = new Vector2(-22f, 0f);
        footDivider.rectTransform.anchoredPosition = new Vector2(0f, -374f);

        var footer = HoloUI.CreateText(panel, "Pie", 12, TextAnchor.UpperLeft, HoloUI.TextDim);
        Anchor(footer.rectTransform, new Vector2(28f, -386f), new Vector2(-56f, 20f));
        footer.text = "↑ ↓ o RATÓN  ELEGIR      E o 2º CLIC  COMPRAR      ESC o CLIC DERECHO  SALIR";
    }

    static void Anchor(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    static void AnchorRight(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }
}
