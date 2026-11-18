using UnityEngine;
using UnityEngine.UI;

// Fundido a negro a pantalla completa. Lo usan las dos puntas de una transición: el
// menú se va a negro y la nave aparece desde negro.
//
// Se dibuja por encima de todo lo demás (orden 200) porque tiene que tapar también los
// paneles holográficos, que ya están altos.
public class ScreenFade : MonoBehaviour
{
    [Tooltip("Arranca en negro y se abre solo. Es lo que va en la escena que RECIBE la transición.")]
    [SerializeField] bool fadeInOnStart;

    [SerializeField] float startDuration = 1.1f;

    CanvasGroup group;
    float target;
    float speed = 1f;

    public bool IsBlack => group != null && group.alpha >= 0.999f;

    void Awake()
    {
        BuildUI();
        group.alpha = fadeInOnStart ? 1f : 0f;
        target = fadeInOnStart ? 0f : group.alpha;
        speed = 1f / Mathf.Max(0.01f, startDuration);
    }

    public void FadeTo(float alpha, float duration)
    {
        target = Mathf.Clamp01(alpha);
        speed = 1f / Mathf.Max(0.01f, duration);
    }

    public void SetImmediate(float alpha)
    {
        target = Mathf.Clamp01(alpha);
        if (group != null) group.alpha = target;
    }

    void Update()
    {
        if (group == null) return;
        group.alpha = Mathf.MoveTowards(group.alpha, target, speed * Time.deltaTime);
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("CanvasFundido");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        group = canvasGO.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        var imageGO = new GameObject("Negro");
        imageGO.transform.SetParent(canvasGO.transform, false);

        var image = imageGO.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        var rt = imageGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
