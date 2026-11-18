using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// La salida del menú. En vez de cortar a la nave, los controles se desvanecen, la
// cámara empieza a derivar hacia la silueta lejana y el fundido la alcanza antes de
// llegar. Nunca se llega a la nave a propósito: de cerca es un diorama abierto por dos
// lados, y con la niebla a esa distancia es una forma y nada más.
//
// La carga corre en paralelo. Con LoadScene normal la cámara se congelaría a mitad del
// movimiento, que es justo lo que esto viene a evitar.
public class MenuLaunchSequence : MonoBehaviour
{
    [SerializeField] string lobbySceneName = "Ship_Lobby";
    [SerializeField] Transform cameraTransform;
    [SerializeField] ScreenFade fade;

    [Tooltip("Música del menú. Se va con la imagen: si se cortara en seco al cargar la nave se notaría más que la propia carga.")]
    [SerializeField] AmbientMusic music;

    [Tooltip("De acá salen los grupos que se apagan. Los crea en su Awake, así que se piden al arrancar la secuencia y no desde el editor.")]
    [SerializeField] MainMenuUI menu;

    CanvasGroup chrome;   // lista, avisos, pie
    CanvasGroup brand;    // título y subtítulo
    CanvasGroup veil;     // velo oscuro sobre el fondo

    [Header("Tiempos")]
    [Tooltip("Lo que tardan en irse los controles. La cámara ya está derivando debajo.")]
    [SerializeField] float chromeFade = 0.7f;

    [Tooltip("Cuánto se queda el título solo, con el fondo despejado, antes de irse.")]
    [SerializeField] float brandHold = 1.5f;

    [SerializeField] float brandFade = 1f;
    [SerializeField] float blackout = 1.2f;

    [Header("Cámara")]
    [Tooltip("Cuánto avanza en total, en unidades.")]
    [SerializeField] float travel = 42f;

    [Tooltip("Duración del avance. Se acelera hacia el final: la deriva arranca antes de que se note.")]
    [SerializeField] float travelDuration = 5.2f;

    bool running;
    bool skipped;

    public void Play()
    {
        if (running) return;
        running = true;

        if (menu != null)
        {
            chrome = menu.ChromeGroup;
            brand = menu.BrandGroup;
            veil = menu.VeilGroup;
        }

        StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        var load = SceneManager.LoadSceneAsync(lobbySceneName);
        if (load == null)
        {
            Debug.LogError($"MenuLaunchSequence: '{lobbySceneName}' no está en el build. " +
                           "Se carga de golpe para no dejar el menú colgado.");
            SceneManager.LoadScene(lobbySceneName);
            yield break;
        }

        load.allowSceneActivation = false;

        // El frame en que arrancó. La corrutina corre hasta el primer yield DENTRO del
        // mismo frame en que se pulsó E, así que sin esto Input.anyKeyDown seguía
        // valiendo true y la secuencia se saltaba a sí misma al instante.
        int startFrame = Time.frameCount;

        Vector3 from = cameraTransform != null ? cameraTransform.position : Vector3.zero;
        Vector3 to = from + (cameraTransform != null ? cameraTransform.forward : Vector3.forward) * travel;

        if (chrome != null) StartCoroutine(FadeGroup(chrome, 0f, chromeFade));

        // La música empieza a irse desde el primer instante y tarda todo el viaje: es
        // más largo que el fundido a negro a propósito, para que no se apague antes que
        // la imagen y quede un tramo mudo mirando la nave.
        if (music != null) music.FadeOut(travelDuration);

        // El velo se abre mientras avanza: el fondo se despeja a medida que te acercas.
        if (veil != null) StartCoroutine(FadeGroup(veil, 0.18f, travelDuration * 0.6f));

        float elapsed = 0f;
        float brandStarted = -1f;
        bool blackoutStarted = false;

        while (elapsed < travelDuration)
        {
            elapsed += Time.deltaTime;

            if (Time.frameCount != startFrame && Input.anyKeyDown) Skip(ref elapsed);

            // Acelera en vez de ir parejo: un avance lineal se lee como un deslizador y
            // no como algo que arranca.
            float t = Mathf.Clamp01(elapsed / travelDuration);
            if (cameraTransform != null)
                cameraTransform.position = Vector3.Lerp(from, to, t * t);

            if (brandStarted < 0f && elapsed >= brandHold)
            {
                brandStarted = elapsed;
                if (brand != null) StartCoroutine(FadeGroup(brand, 0f, brandFade));
            }

            // El fundido arranca antes de que termine el avance, para que la pantalla
            // ya esté negra cuando la cámara se detiene. Si se detuviera a la vista se
            // notaría el freno.
            if (!blackoutStarted && elapsed >= travelDuration - blackout)
            {
                blackoutStarted = true;
                fade?.FadeTo(1f, blackout);
            }

            yield return null;
        }

        if (!blackoutStarted) fade?.FadeTo(1f, blackout);

        // Se espera a las dos cosas: que la pantalla esté negra y que la escena esté
        // lista. Normalmente la nave carga mucho antes, así que manda el fundido.
        while ((fade != null && !fade.IsBlack) || load.progress < 0.9f)
            yield return null;

        load.allowSceneActivation = true;
    }

    // Saltar no corta a negro de golpe: comprime lo que queda. Un corte instantáneo se
    // siente como un error, y esto se siente como adelantar.
    void Skip(ref float elapsed)
    {
        if (skipped) return;
        skipped = true;

        float remaining = travelDuration - elapsed;
        if (remaining <= blackout) return;

        elapsed = travelDuration - blackout;
        if (brand != null) StartCoroutine(FadeGroup(brand, 0f, 0.25f));
    }

    static IEnumerator FadeGroup(CanvasGroup group, float target, float duration)
    {
        float start = group.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = target;
    }
}
