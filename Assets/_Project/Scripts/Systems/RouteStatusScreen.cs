using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Pantalla de pared con el estado de la ruta de Paralax. No es decoración: lee
// RouteDatabase de verdad, así que marca dónde estás parado en el contrato.
public class RouteStatusScreen : MonoBehaviour
{
    Text bodyText;

    void Awake()
    {
        BuildUI();
    }

    void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        var bodies = RouteDatabase.Bodies;
        if (bodies.Length == 0)
        {
            bodyText.text = "Sin ruta asignada.";
            return;
        }

        int current = RouteDatabase.CurrentIndex;
        var sb = new StringBuilder();

        for (int i = 0; i < bodies.Length; i++)
        {
            string mark = i < current ? "·" : (i == current ? ">" : " ");
            string state = i < current ? "RELEVADO" : (i == current ? "ASIGNADO" : "PENDIENTE");
            sb.AppendLine($"{mark} {bodies[i].nombre,-9} {bodies[i].clase,-22} {state}");
        }

        sb.AppendLine();
        sb.AppendLine($"Progreso de contrato: {current} de {bodies.Length} cuerpos.");
        bodyText.text = sb.ToString();
    }

    void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var canvasRT = GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(560f, 400f);

        var panel = HoloUI.CreatePanel(transform, "Panel", new Vector2(540f, 380f), Vector2.zero,
                                       new Vector2(0.5f, 0.5f), HoloUI.FillDeep);

        var header = HoloUI.CreateText(panel, "Cabecera", 24, TextAnchor.UpperLeft, HoloUI.Line);
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = new Vector2(1f, 1f);
        header.rectTransform.pivot = new Vector2(0f, 1f);
        header.rectTransform.sizeDelta = new Vector2(-48f, 30f);
        header.rectTransform.anchoredPosition = new Vector2(24f, -20f);
        header.text = "REGISTRO DE RUTA";

        var divider = HoloUI.CreateDivider(panel, 1f, HoloUI.LineDim);
        divider.rectTransform.offsetMin = new Vector2(20f, 0f);
        divider.rectTransform.offsetMax = new Vector2(-20f, 0f);
        divider.rectTransform.anchoredPosition = new Vector2(0f, -56f);

        bodyText = HoloUI.CreateText(panel, "Cuerpo", 15, TextAnchor.UpperLeft, HoloUI.Text);
        bodyText.rectTransform.anchorMin = Vector2.zero;
        bodyText.rectTransform.anchorMax = Vector2.one;
        bodyText.rectTransform.offsetMin = new Vector2(24f, 20f);
        bodyText.rectTransform.offsetMax = new Vector2(-24f, -68f);
    }
}
