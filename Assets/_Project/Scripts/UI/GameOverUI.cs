using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] string lobbySceneName = "Ship_Lobby";

    CanvasGroup group;
    bool shown;
    bool subscribed;

    // El componente vive en la escena desde el principio, así que su 'enabled' está
    // siempre en true y no sirve para saber si la pantalla se está viendo. Quien
    // necesite preguntarlo —la pausa, para no abrirse encima— tiene que mirar esto.
    public bool IsShown => shown;

    void Awake()
    {
        BuildUI();
        group.alpha = 0f;
    }

    void Start()
    {
        if (PlayerHealth.Instance == null) return;
        PlayerHealth.Instance.OnDeath += Show;
        subscribed = true;
    }

    void OnDestroy()
    {
        if (subscribed && PlayerHealth.Instance != null)
            PlayerHealth.Instance.OnDeath -= Show;
    }

    void Update()
    {
        if (!shown) return;

        group.alpha = Mathf.MoveTowards(group.alpha, 1f, Time.deltaTime * 1.1f);

        // Morir devuelve a la nave, no reinicia el cuerpo. Las fichas de la jornada se
        // pierden porque solo se registran al despegar, y la ruta NO avanza: el cuerpo
        // sigue asignado y hay que volver a bajar. Se pierde el trabajo, no la partida.
        // Clic también: en la pantalla de muerte el cursor está suelto y visible, y
        // pedir una tecla concreta mientras se ve un puntero es confuso.
        if (Input.GetKeyDown(KeyCode.R) || Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(lobbySceneName);
        }
    }

    void Show()
    {
        shown = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("GameOverCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvasGO.AddComponent<CanvasScaler>();
        group = canvasGO.AddComponent<CanvasGroup>();

        var fadeGO = new GameObject("Fade");
        fadeGO.transform.SetParent(canvasGO.transform, false);
        var fade = fadeGO.AddComponent<Image>();
        fade.color = new Color(0.016f, 0.008f, 0.047f, 0.96f);
        var fadeRT = fadeGO.GetComponent<RectTransform>();
        fadeRT.anchorMin = Vector2.zero;
        fadeRT.anchorMax = Vector2.one;
        fadeRT.offsetMin = Vector2.zero;
        fadeRT.offsetMax = Vector2.zero;

        CreateLabel(canvasGO.transform, "Title", "Paralax da la jornada por perdida.", 34,
                    new Color(0.94f, 0.45f, 0.42f), new Vector2(0f, 40f));

        CreateLabel(canvasGO.transform, "Hint", "R o CLIC  —  volver a la nave", 17,
                    new Color(0.435f, 0.788f, 0.831f), new Vector2(0f, -30f));
    }

    static void CreateLabel(Transform parent, string name, string content, int size, Color color, Vector2 offset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = HoloUI.Font;
        text.text = content;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(700f, 60f);
        rt.anchoredPosition = offset;
    }
}
