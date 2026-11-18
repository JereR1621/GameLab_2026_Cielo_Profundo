using System.Collections;
using UnityEngine;

// La araña te atrapa: se encabrita sobre ti, la cámara baja a mirarla desde abajo,
// y hay que machacar E para llenar la barra y zafarse. Al lograrlo queda aturdida.
public class SpiderCatchSequence : MonoBehaviour
{
    public static SpiderCatchSequence Instance { get; private set; }

    [SerializeField] Transform player;
    [SerializeField] PlayerController playerController;
    [SerializeField] PlayerCamera playerCamera;
    [SerializeField] PlayerAnimator playerAnimator;
    [SerializeField] StruggleBarUI bar;

    [Tooltip("El rugido de la captura. Dura casi seis segundos porque el forcejeo dura hasta nueve: uno corto se apagaría a mitad de la escena y te dejaría peleando en silencio.")]
    [SerializeField] AudioClip catchClip;

    [SerializeField, Range(0f, 1f)] float catchVolume = 0.9f;

    [Header("Encuadre")]
    [Tooltip("La cámara se pone baja y mira hacia arriba: es lo que hace que la araña se vea encima tuyo.")]
    [SerializeField] float cameraDistance = 3.2f;
    [SerializeField] float cameraHeight = 0.7f;
    [SerializeField] float cameraSideOffset = 1.1f;
    [SerializeField] float spiderStandoff = 2.1f;

    [Header("Forcejeo")]
    [SerializeField] float pressAmount = 0.085f;
    [SerializeField] float decayPerSecond = 0.3f;
    [Tooltip("Si no llenas la barra en este tiempo, pierdes un corazón.")]
    [SerializeField] float struggleTimeLimit = 9f;
    [Tooltip("Aturdimiento corto cuando fallas: igual te suelta, pero te da mucho menos margen.")]
    [SerializeField] float failStunDuration = 2.5f;

    [Header("Tiempos")]
    [Tooltip("La entrada larga se reproduce solo la primera vez; después se va directo al forcejeo.")]
    [SerializeField] float firstTimeIntro = 0.85f;
    [SerializeField] float shortIntro = 0.3f;
    [SerializeField] float releaseDuration = 0.5f;

    bool hasBeenCaughtBefore;

    public bool IsPlaying { get; private set; }

    AudioSource source;

    void Awake()
    {
        Instance = this;

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0.2f;
        source.dopplerLevel = 0f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void TryCatch(SpiderCreature spider)
    {
        if (IsPlaying || spider == null || spider.IsStunned) return;

        var health = PlayerHealth.Instance;
        if (health != null && (health.IsDead || health.IsInvulnerable)) return;

        // Si te pilla escondido, primero te saca. Va acá y no en quien dispara la captura
        // para que cualquier camino que acabe en forcejeo arranque del mismo estado
        // limpio, con el jugador fuera del pozo y el control ya devuelto.
        PlayerHiding.Instance?.ForceRelease();

        StartCoroutine(Sequence(spider));
    }

    // Casi en 2D y SIN reverb. La araña está encima tuyo, así que espacializarlo no
    // aporta dirección; y el clip ya lleva cinco segundos de cola propia, de manera que
    // añadirle reverb encima solo lo emborronaría. El tamaño ya lo cuenta la cola.
    IEnumerator Sequence(SpiderCreature spider)
    {
        IsPlaying = true;
        spider.BeginCapture();

        if (source != null && catchClip != null)
            source.PlayOneShot(catchClip, catchVolume * GameSettings.EffectsVolume);

        if (playerController != null) playerController.enabled = false;
        if (playerCamera != null) playerCamera.InputLocked = true;
        if (playerAnimator != null) playerAnimator.Struggling = true;

        // La araña se planta delante tuyo y te encara.
        Vector3 fromPlayer = spider.transform.position - player.position;
        fromPlayer.y = 0f;
        if (fromPlayer.sqrMagnitude < 0.01f) fromPlayer = player.forward;
        Vector3 spiderSpot = player.position + fromPlayer.normalized * spiderStandoff;

        float intro = hasBeenCaughtBefore ? shortIntro : firstTimeIntro;
        hasBeenCaughtBefore = true;

        Vector3 spiderStart = spider.transform.position;
        Quaternion spiderStartRot = spider.transform.rotation;
        Quaternion spiderFacing = Quaternion.LookRotation(-fromPlayer.normalized, Vector3.up);

        float t = 0f;
        while (t < intro)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / intro);

            Vector3 target = new Vector3(spiderSpot.x, spider.transform.position.y, spiderSpot.z);
            spider.transform.position = Vector3.Lerp(
                new Vector3(spiderStart.x, spider.transform.position.y, spiderStart.z), target, k);
            spider.transform.rotation = Quaternion.Slerp(spiderStartRot, spiderFacing, k);

            ApplyCinematicPose(k, spider);
            yield return null;
        }

        bar.Show();

        float progress = 0f;
        float elapsed = 0f;
        while (progress < 1f && elapsed < struggleTimeLimit)
        {
            elapsed += Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.E)) progress += pressAmount;
            progress -= decayPerSecond * Time.deltaTime;
            progress = Mathf.Clamp01(progress);

            bar.SetProgress(progress);
            spider.transform.rotation = spiderFacing;
            ApplyCinematicPose(1f, spider);
            yield return null;
        }

        bool escaped = progress >= 1f;
        bar.Hide();

        // Te suelta en los dos casos, pero fallar cuesta un corazón y te deja
        // mucho menos margen para huir.
        spider.EndCapture();
        spider.Stun(escaped ? 0f : failStunDuration);
        if (playerAnimator != null) playerAnimator.Struggling = false;

        if (!escaped) PlayerHealth.Instance?.LoseHeart();

        Vector3 knockFrom = spider.transform.position;
        Vector3 knockTo = knockFrom + fromPlayer.normalized * 2.2f;

        t = 0f;
        while (t < releaseDuration)
        {
            t += Time.deltaTime;
            float k = t / releaseDuration;

            Vector3 knocked = Vector3.Lerp(knockFrom, knockTo, Mathf.SmoothStep(0f, 1f, k));
            spider.transform.position = new Vector3(knocked.x, spider.transform.position.y, knocked.z);

            ApplyCinematicPose(1f - k, spider);
            yield return null;
        }

        if (playerCamera != null)
        {
            playerCamera.CinematicBlend = 0f;
            playerCamera.InputLocked = false;
        }

        // Si ese fue el último corazón, el control no vuelve: queda la pantalla final.
        bool dead = PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead;
        if (playerController != null && !dead) playerController.enabled = true;

        IsPlaying = false;
    }

    void ApplyCinematicPose(float blend, SpiderCreature spider)
    {
        if (playerCamera == null) return;

        Vector3 focus = spider.transform.position + Vector3.up * 0.6f;
        Vector3 away = (spider.transform.position - player.position);
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = player.forward;
        away.Normalize();

        Vector3 camPos = player.position
                         - away * cameraDistance
                         + Vector3.Cross(Vector3.up, away) * cameraSideOffset
                         + Vector3.up * cameraHeight;

        playerCamera.CinematicPosition = camPos;
        playerCamera.CinematicRotation = Quaternion.LookRotation(focus - camPos);
        playerCamera.CinematicBlend = Mathf.Clamp01(blend);
    }
}
