using System.Collections;
using UnityEngine;

// Los dos avisos de la araña: el acercamiento a la cara cuando te detecta, y el
// plano de ella alejándose cuando te pierde.
//
// Las dos tienen tiempo de espera entre repeticiones a propósito. Que te detecte
// puede pasar decenas de veces por nivel, y una cinemática cada vez dejaría de dar
// miedo para pasar a ser un peaje — encima quitándote el control justo cuando
// necesitas correr.
public class SpiderReactionSequence : MonoBehaviour
{
    public static SpiderReactionSequence Instance { get; private set; }

    [SerializeField] Transform player;
    [SerializeField] PlayerController playerController;
    [SerializeField] PlayerCamera playerCamera;

    [Header("Detección")]
    [Tooltip("Cuánto tarda la cámara en llegar hasta la cara.")]
    [SerializeField] float detectEnter = 0.3f;
    [Tooltip("Cuánto se queda quieta EN la cara. Este es el tiempo que se ve de verdad.")]
    [SerializeField] float detectHold = 1.1f;
    [Tooltip("Cuánto tarda en volver a la cámara normal.")]
    [SerializeField] float detectExit = 0.35f;
    [SerializeField] float detectCooldown = 25f;
    [SerializeField] float faceDistance = 2.2f;
    [SerializeField] float faceHeight = 0.35f;
    [Tooltip("Inclinación de cámara. Un plano torcido incomoda sin que se sepa por qué.")]
    [SerializeField] float dutchAngle = 9f;

    [Header("Pérdida: puedes mirar tranquilo, ya estás a salvo")]
    [SerializeField] float loseEnter = 0.4f;
    [SerializeField] float loseHold = 1.4f;
    [SerializeField] float loseExit = 0.5f;
    [SerializeField] float loseCooldown = 25f;
    [Tooltip("Más lejos que esto no se muestra el plano: la niebla no dejaría ver nada.")]
    [SerializeField] float loseMaxDistance = 34f;
    [SerializeField] float loseCameraBack = 3.4f;
    [SerializeField] float loseCameraHeight = 2.2f;

    float lastDetectTime = -999f;
    float lastLoseTime = -999f;

    public bool IsPlaying { get; private set; }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    bool CanPlay()
    {
        if (IsPlaying) return false;
        if (SpiderCatchSequence.Instance != null && SpiderCatchSequence.Instance.IsPlaying) return false;
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) return false;
        return true;
    }

    public void PlayDetected(SpiderCreature spider)
    {
        if (!CanPlay() || Time.time - lastDetectTime < detectCooldown) return;
        lastDetectTime = Time.time;
        StartCoroutine(DetectedRoutine(spider));
    }

    public void PlayLostTrack(SpiderCreature spider)
    {
        if (spider == null) return;

        // Se retira siempre, se vea o no. Lo que depende de la distancia es el plano.
        spider.Retreat(spider.transform.position - player.position, 6f);

        // Con esta niebla, mirarla desde 60 unidades es mirar la nada: el plano se
        // reproduciría sobre una pantalla vacía. Solo vale la pena si está cerca.
        if (Vector3.Distance(spider.transform.position, player.position) > loseMaxDistance) return;
        if (!CanPlay() || Time.time - lastLoseTime < loseCooldown) return;

        lastLoseTime = Time.time;
        StartCoroutine(LostTrackRoutine(spider));
    }

    IEnumerator DetectedRoutine(SpiderCreature spider)
    {
        IsPlaying = true;
        if (playerController != null) playerController.enabled = false;
        if (playerCamera != null) playerCamera.InputLocked = true;
        if (spider != null) spider.FrozenByCinematic = true;

        // Tres fases separadas en vez de una curva de ida y vuelta: si no, la cámara
        // toca la cara un solo instante y la pausa que se quiere ver no existe.
        yield return Blend(detectEnter, detectHold, detectExit, blend =>
        {
            if (spider == null) return false;

            Vector3 focus = spider.transform.position + Vector3.up * 0.25f;
            Vector3 camPos = focus + spider.transform.forward * faceDistance + Vector3.up * faceHeight;

            playerCamera.CinematicPosition = camPos;
            playerCamera.CinematicRotation =
                Quaternion.LookRotation(focus - camPos) * Quaternion.Euler(0f, 0f, dutchAngle);
            playerCamera.CinematicBlend = blend;
            return true;
        });

        if (spider != null) spider.FrozenByCinematic = false;
        Release();
    }

    IEnumerator LostTrackRoutine(SpiderCreature spider)
    {
        IsPlaying = true;
        if (playerController != null) playerController.enabled = false;
        if (playerCamera != null) playerCamera.InputLocked = true;

        // Acá no se congela: la retirada ya se disparó en PlayLostTrack y todo el
        // sentido del plano es verla caminar hacia el otro lado.
        yield return Blend(loseEnter, loseHold, loseExit, blend =>
        {
            if (spider == null) return false;

            // Por encima del hombro, mirándola irse: es el plano que dice "ya está".
            Vector3 toSpider = spider.transform.position - player.position;
            toSpider.y = 0f;
            if (toSpider.sqrMagnitude < 0.01f) toSpider = player.forward;
            toSpider.Normalize();

            Vector3 camPos = player.position - toSpider * loseCameraBack + Vector3.up * loseCameraHeight;
            Vector3 focus = spider.transform.position + Vector3.up * 0.4f;

            playerCamera.CinematicPosition = camPos;
            playerCamera.CinematicRotation = Quaternion.LookRotation(focus - camPos);
            playerCamera.CinematicBlend = blend;
            return true;
        });

        // La retirada sigue después de que termina el plano: se la ve alejarse un
        // rato más aunque la cámara ya haya vuelto.
        Release();
    }

    // Entrada, pausa y salida como tramos separados. El callback devuelve false si
    // el objetivo desapareció, para cortar sin dejar la cámara colgada.
    IEnumerator Blend(float enter, float hold, float exit, System.Func<float, bool> apply)
    {
        float t = 0f;
        while (t < enter)
        {
            t += Time.deltaTime;
            if (!apply(Mathf.SmoothStep(0f, 1f, t / enter))) yield break;
            yield return null;
        }

        t = 0f;
        while (t < hold)
        {
            t += Time.deltaTime;
            if (!apply(1f)) yield break;
            yield return null;
        }

        t = 0f;
        while (t < exit)
        {
            t += Time.deltaTime;
            if (!apply(1f - Mathf.SmoothStep(0f, 1f, t / exit))) yield break;
            yield return null;
        }
    }

    void Release()
    {
        if (playerCamera != null)
        {
            playerCamera.CinematicBlend = 0f;
            playerCamera.InputLocked = false;
        }

        // Ojo: la captura puede haber arrancado durante la cinemática. Si es así,
        // devolver el control acá le pisaría el congelamiento a esa secuencia.
        bool caught = SpiderCatchSequence.Instance != null && SpiderCatchSequence.Instance.IsPlaying;
        bool dead = PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead;

        if (playerController != null && !caught && !dead) playerController.enabled = true;
        IsPlaying = false;
    }
}
