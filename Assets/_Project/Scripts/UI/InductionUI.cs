using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// La inducción de PARALAX: quién te contrató y a qué bajas.
//
// Se ve al entrar a la nave por primera vez, antes de tocar nada. Hasta ahora el juego
// empezaba con el jugador de pie en un pasillo, sin que nadie le hubiera dicho para
// quién trabaja ni qué se supone que tiene que hacer: el bucle entero —bajar, recoger,
// transmitir, cobrar— se deducía a base de probar. En una jam eso son los cinco minutos
// que tienes, gastados en averiguar el juego en vez de en jugarlo.
//
// Va en el mismo negro sobre blanco que las fichas del descenso, para que se lea como
// papeleo de la empresa y no como un tutorial pegado por fuera.
//
// Además hace el trabajo de fondo: las dos últimas pantallas plantan que eres la sexta
// cuadrilla y que nadie dice qué pasó con las cinco anteriores, que es la pregunta que
// el resto del juego va contestando ficha a ficha.
public class InductionUI : MonoBehaviour
{
    [SerializeField] PlayerController playerController;
    [SerializeField] PlayerCamera playerCamera;

    [Tooltip("Cuánto tarda cada bloque en aparecer y en irse.")]
    [SerializeField] float fadeDuration = 1.1f;

    [Tooltip("Cuánto se queda cada bloque antes de irse. Corto: se puede leer entero y adelantar en cualquier momento.")]
    [SerializeField] float holdDuration = 5.6f;

    GameObject root;
    Text label, hint;
    CanvasGroup group;

    void Start()
    {
        // Una vez por ruta. Repetirla en cada visita a la nave sería un peaje: la
        // información ya la tienes, y volver a leerla en cada despegue cansa a la
        // segunda vez.
        if (Expedition.InductionSeen)
        {
            enabled = false;
            return;
        }

        BuildUI();

        if (playerController != null) playerController.enabled = false;
        if (playerCamera != null) playerCamera.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;

        StartCoroutine(Secuencia());
    }

    string[] Bloques()
    {
        int cuerpos = RouteDatabase.Bodies.Length;

        return new[]
        {
            "PARALAX",

            "Bienvenido, operario.\n\n" +
            "PARALAX recupera instrumental y restos de valor científico en cuerpos ya " +
            "catalogados. Tú bajas, recoges y transmites. Nosotros archivamos.",

            $"Se te asigna una ruta de {cuerpos} cuerpos. Cada uno tiene su catálogo de " +
            "piezas, y el catálogo se cierra entero o no se cierra.\n\n" +
            "El módulo de ascenso no despega mientras quede una pieza abajo.",

            "Antes de cada descenso, identifica el cuerpo con la esfera armilar de la nave.\n\n" +
            "Bajar sin identificar está permitido. No lo recomendamos.",

            "Eres la sexta cuadrilla asignada a esta ruta.\n\n" +
            "No consta por qué las cinco anteriores no la cerraron.",

            "Gracias por tu compromiso.",
        };
    }

    IEnumerator Secuencia()
    {
        var bloques = Bloques();

        for (int i = 0; i < bloques.Length; i++)
        {
            // El primero es el nombre de la empresa: grande y centrado, como una placa.
            bool esTitulo = i == 0;
            label.fontSize = esTitulo ? 64 : 22;
            label.alignment = esTitulo ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            label.text = esTitulo ? HoloUI.Espaciar(bloques[i]) : bloques[i];

            yield return Fundir(0f, 1f);

            // Se adelanta solo durante la espera, nunca a mitad de fundido: saltar en la
            // transición hace parpadear la pantalla.
            float t = 0f;
            while (t < holdDuration)
            {
                if (Saltar()) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return Fundir(1f, 0f);
        }

        Cerrar();
    }

    void Cerrar()
    {
        Expedition.InductionSeen = true;
        root.SetActive(false);

        if (playerController != null) playerController.enabled = true;
        if (playerCamera != null) playerCamera.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        UISound.Click();
        enabled = false;
    }

    static bool Saltar() =>
        Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
        Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E);

    IEnumerator Fundir(float desde, float hasta)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(desde, hasta, t / fadeDuration);
            yield return null;
        }

        group.alpha = hasta;
    }

    void BuildUI()
    {
        root = new GameObject("CanvasInduccion");
        root.transform.SetParent(transform, false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Por encima del fundido de entrada de la nave, que está en 200: si no, el lobby
        // se abriría desde negro por delante de la bienvenida.
        canvas.sortingOrder = 240;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // El negro va fuera del grupo que se funde: fundiéndose con el texto, la nave
        // asomaría entre bloque y bloque.
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
        rt.sizeDelta = new Vector2(1000f, 460f);
        rt.anchoredPosition = Vector2.zero;

        label = HoloUI.CreateText(texto.transform, "Linea", 22, TextAnchor.MiddleLeft, Color.white);
        label.lineSpacing = 1.5f;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        HoloUI.Stretch(label.rectTransform);

        // El aviso de que se puede adelantar queda fijo y fuera del grupo que se funde.
        // Dentro parpadearía con cada bloque, y entonces el jugador no sabría si puede
        // saltar o si es parte de la animación.
        hint = HoloUI.CreateText(root.transform, "Pie", 15, TextAnchor.LowerCenter,
                                 new Color(0.5f, 0.53f, 0.58f));
        var hrt = hint.rectTransform;
        hrt.anchorMin = new Vector2(0.5f, 0f);
        hrt.anchorMax = new Vector2(0.5f, 0f);
        hrt.pivot = new Vector2(0.5f, 0f);
        hrt.sizeDelta = new Vector2(700f, 26f);
        hrt.anchoredPosition = new Vector2(0f, 60f);
        hint.text = "ENTER   continuar";
    }
}
