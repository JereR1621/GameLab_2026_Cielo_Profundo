using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// El cierre de la ruta. Se dispara al despegar del último cuerpo con algo transmitido.
//
// Existe porque hasta ahora el juego no terminaba: cerrabas el catálogo en PRL-000,
// despegabas, y volvías a la nave como cualquier otro día, con el último cuerpo todavía
// disponible para bajar otra vez. Todo el lore construye hacia «ya sabe dónde estamos»
// y después no pasaba nada.
//
// Mismo formato negro que las fichas de inicio, para que se lea como parte del mismo
// papeleo de Paralax y no como una pantalla de felicitación.
//
// El remate es el que ya estaba plantado en las fichas y nunca se cobraba: la credencial
// del tramo 3 es la tuya, con años de uso encima, encontrada el día que llegaste. Acá se
// explica por qué — la próxima cuadrilla va a encontrar tus cosas igual que tú
// encontraste las de la quinta.
public class EndingUI : MonoBehaviour
{
    [SerializeField] string menuSceneName = "Main_Menu";

    [Tooltip("Cuánto tarda cada bloque en aparecer y en irse.")]
    [SerializeField] float fadeDuration = 1.6f;

    [Tooltip("Cuánto se queda cada bloque a plena vista antes de irse.")]
    [SerializeField] float holdDuration = 4.2f;

    GameObject root;
    Text label;
    CanvasGroup group;
    bool running;

    public static bool Running { get; private set; }

    public void Play()
    {
        if (running) return;

        running = true;
        Running = true;

        Expedition.RouteCompleted = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;

        // El mundo se congela. Detrás de la pantalla negra el planeta sigue existiendo:
        // el jugador sigue de pie junto al módulo y las arañas siguen oyéndolo, así que
        // sin esto una podía alcanzarte a mitad del cierre y montar la pantalla de muerte
        // encima del final de la ruta.
        //
        // Se congela el reloj en vez de ir desactivando componentes uno por uno: parar el
        // tiempo detiene la simulación entera de una sola vez, sin dejarse nada. Y esta
        // secuencia está escrita con tiempo sin escalar justamente para poder hacerlo.
        Time.timeScale = 0f;

        BuildUI();
        StartCoroutine(Secuencia());
    }

    string[] Bloques()
    {
        int fichas = Expedition.Recovered.Count;
        int cuerpos = RouteDatabase.Bodies.Length;
        string primero = cuerpos > 0 ? RouteDatabase.Bodies[0].nombre : "PRL-114";

        return new[]
        {
            "PARALAX — Cartografía Estelar\n\n" +
            $"Catálogo cerrado. {fichas} entradas registradas.\n" +
            "Contrato cumplido.",

            "El expediente se archiva con tu número de folio.\n\n" +
            "La sexta cuadrilla queda cerrada.",

            "Se emite una ruta nueva.\n\n" +
            $"{cuerpos} cuerpos. El primero es {primero}.\n" +
            "La séptima cuadrilla desciende mañana.",

            "En el primer cuerpo de esa ruta van a encontrar una lámpara encendida junto " +
            "a un puesto de observación, una lente calibrada y guardada con cuidado, y una " +
            "credencial de campo.\n\n" +
            "El plástico rajado. Los bordes redondeados por años de uso.",

            "Tu foto.",

            "CIELO PROFUNDO",
        };
    }

    IEnumerator Secuencia()
    {
        var bloques = Bloques();

        for (int i = 0; i < bloques.Length; i++)
        {
            // El último es el título: más grande y centrado, no un párrafo más.
            bool esTitulo = i == bloques.Length - 1;
            label.fontSize = esTitulo ? 52 : 22;
            label.alignment = esTitulo ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            label.text = esTitulo ? HoloUI.Espaciar(bloques[i]) : bloques[i];

            yield return Fundir(0f, 1f);

            // Se puede adelantar, pero solo durante la espera y nunca durante el fundido:
            // dejar saltar a media transición hace que la pantalla parpadee.
            float t = 0f;
            while (t < holdDuration)
            {
                if (Saltar()) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return Fundir(1f, 0f);
        }

        // El reloj vuelve ANTES de cargar: un menú que arranca con timeScale en cero se
        // queda congelado y no responde a nada.
        Time.timeScale = 1f;
        Running = false;
        SceneManager.LoadScene(menuSceneName);
    }

    // Red de seguridad, igual que en la recogida: si la escena cambia con el cierre a
    // medias, el reloj se quedaría en cero y el menú arrancaría congelado.
    void OnDisable()
    {
        if (Running) { Running = false; Time.timeScale = 1f; }
    }

    static bool Saltar() =>
        Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
        Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E);

    IEnumerator Fundir(float desde, float hasta)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            // Sin escalar por el tiempo del juego: si algo dejó timeScale tocado, el
            // cierre se vería a cámara lenta o directamente congelado.
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(desde, hasta, t / fadeDuration);
            yield return null;
        }

        group.alpha = hasta;
    }

    void BuildUI()
    {
        root = new GameObject("CanvasFinal");
        root.transform.SetParent(transform, false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 320;   // por encima incluso de la pausa

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // El fondo negro va fuera del grupo que se funde: si se fundiera con el texto, el
        // planeta asomaría entre bloque y bloque.
        var fondo = new GameObject("Fondo");
        fondo.transform.SetParent(root.transform, false);
        var fondoImg = fondo.AddComponent<Image>();
        fondoImg.color = Color.black;
        fondoImg.raycastTarget = false;
        HoloUI.Stretch(fondo.GetComponent<RectTransform>());

        // Con RectTransform desde el principio, y esto no es cosmético: CanvasGroup NO
        // lo arrastra. Creando el objeto pelado, el GetComponent de abajo devolvía null
        // y la línea siguiente reventaba — después de haber creado ya el fondo negro y
        // antes de crear el texto y arrancar la secuencia. El resultado era pantalla
        // negra para siempre, con el sonido corriendo por detrás y nada que mirar.
        var texto = new GameObject("Texto", typeof(RectTransform));
        texto.transform.SetParent(root.transform, false);
        group = texto.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var rt = (RectTransform)texto.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1000f, 500f);
        rt.anchoredPosition = Vector2.zero;

        label = HoloUI.CreateText(texto.transform, "Linea", 22, TextAnchor.MiddleLeft, Color.white);
        label.lineSpacing = 1.5f;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        HoloUI.Stretch(label.rectTransform);
    }
}
