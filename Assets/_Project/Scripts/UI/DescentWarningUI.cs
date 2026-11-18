using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Aviso al aterrizar cuando el descenso viene penalizado. Sin esto la dificultad
// multiplicada era invisible: bajabas, había más arañas, y no había forma de saber si
// era por algo que hiciste o si el planeta era así.
//
// Aparece después del comunicado de Paralax, no encima. Y en ámbar, no en cian: el
// resto de la interfaz de la nave es informativa, esto es una advertencia.
public class DescentWarningUI : MonoBehaviour
{
    [Tooltip("Cuánto espera antes de aparecer, para no pisar el comunicado de llegada.")]
    [SerializeField] float delaySeconds = 3.5f;

    [SerializeField] float holdSeconds = 9f;
    [SerializeField] float fadeSpeed = 1.8f;

    CanvasGroup group;
    bool cierreAvisado;
    Text title, detail, factor;

    void Awake()
    {
        BuildUI();
        group.alpha = 0f;
    }

    void Start()
    {
        float difficulty = Expedition.DescentDifficulty;
        if (difficulty <= 1.001f) return;

        Anunciar("DESCENSO SIN IDENTIFICAR",
                 "El estudio de PARALAX se descartó tres veces. Bajaste sin saber\n"
                 + "a qué cuerpo, y Paralax registró el descenso igual.",
                 $"ACTIVIDAD ESTIMADA  ×{difficulty:0.#}",
                 delaySeconds);
    }

    // Cierre del catálogo: al levantar la última pieza, el planeta se entera.
    //
    // Este aviso no es cortesía, es lo que hace justo el pico de dificultad. Al completar
    // el catálogo, todas las arañas reciben tu última posición conocida; sin decírtelo,
    // te encontrarías de golpe con media superficie encima y parecería que el juego hace
    // trampa. Dicho, es una regla: has terminado, ahora sal de ahí.
    //
    // Y de paso resuelve algo que faltaba desde que recoger todo es obligatorio: hasta
    // ahora nada te decía que ya podías irte, salvo un contador en la esquina.
    void Update()
    {
        if (cierreAvisado) return;
        if (Inventory.Instance == null || !Inventory.Instance.IsComplete) return;

        cierreAvisado = true;

        // Sin espera: llega en el momento exacto de recoger la última.
        Anunciar("CATÁLOGO COMPLETO",
                 "Ya no queda nada que recuperar en este cuerpo.\n"
                 + "Vuelve al módulo de ascenso.",
                 "ACTIVIDAD EN AUMENTO",
                 0f);
    }

    void Anunciar(string titulo, string detalle, string pie, float espera)
    {
        title.text = titulo;
        detail.text = detalle;
        factor.text = pie;

        // Se corta cualquier aviso anterior a medias: dos textos fundiéndose a la vez en
        // el mismo lienzo se leerían encimados.
        StopAllCoroutines();
        StartCoroutine(PlayOnce(espera));
    }

    IEnumerator PlayOnce(float espera)
    {
        if (espera > 0f) yield return new WaitForSeconds(espera);

        while (group.alpha < 1f)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 1f, Time.deltaTime * fadeSpeed);
            yield return null;
        }

        yield return new WaitForSeconds(holdSeconds);

        while (group.alpha > 0f)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.deltaTime * fadeSpeed);
            yield return null;
        }
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("CanvasAviso");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;
        canvasGO.AddComponent<CanvasScaler>();
        group = canvasGO.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // Abajo y centrado, lejos del comunicado de llegada, que va arriba.
        var panel = HoloUI.CreatePanel(canvasGO.transform, "PanelAviso",
                                       new Vector2(620f, 128f), new Vector2(0f, 150f),
                                       new Vector2(0.5f, 0f), new Color(0.10f, 0.04f, 0.01f, 0.92f));

        var border = new GameObject("Borde");
        border.transform.SetParent(panel, false);
        var borderImage = border.AddComponent<Image>();
        borderImage.sprite = HoloUI.PanelBorder;
        borderImage.type = Image.Type.Sliced;
        borderImage.color = HoloUI.Accent;
        borderImage.raycastTarget = false;
        HoloUI.Stretch(border.GetComponent<RectTransform>());

        title = HoloUI.CreateText(panel, "Titulo", 17, TextAnchor.UpperLeft, HoloUI.Accent);
        Place(title.rectTransform, new Vector2(20f, -16f), new Vector2(420f, 24f));

        factor = HoloUI.CreateText(panel, "Factor", 15, TextAnchor.UpperRight, new Color(1f, 0.72f, 0.42f));
        var fRT = factor.rectTransform;
        fRT.anchorMin = fRT.anchorMax = new Vector2(1f, 1f);
        fRT.pivot = new Vector2(1f, 1f);
        fRT.sizeDelta = new Vector2(280f, 24f);
        fRT.anchoredPosition = new Vector2(-20f, -16f);

        detail = HoloUI.CreateText(panel, "Detalle", 13, TextAnchor.UpperLeft, new Color(0.82f, 0.70f, 0.58f));
        Place(detail.rectTransform, new Vector2(20f, -52f), new Vector2(580f, 60f));
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }
}
