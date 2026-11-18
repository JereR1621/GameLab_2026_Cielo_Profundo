using System.Collections;
using UnityEngine;

// Secuencia de recogida al estilo Breath of the Wild: el personaje levanta el objeto
// sobre la cabeza con las dos manos, la cámara se va al frente para encuadrarlo, y
// una carta muestra qué es y de dónde vino. El control se devuelve al confirmar.
public class ItemPickupSequence : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] PlayerController playerController;
    [SerializeField] PlayerCamera playerCamera;
    [SerializeField] PlayerAnimator playerAnimator;
    [SerializeField] ItemCardUI card;

    [Header("Encuadre")]
    [SerializeField] float holdHeight = 2.3f;
    [SerializeField] float cameraDistance = 2.4f;
    [SerializeField] float cameraSideOffset = 0.6f;
    [SerializeField] float cameraHeight = 1.8f;

    [Header("Sonido")]
    [Tooltip("Suena al levantar la pieza, no al cerrar la carta: el momento en que sientes que la tomaste es cuando la levantas.")]
    [SerializeField] AudioClip pickupClip;

    [SerializeField, Range(0f, 1f)] float pickupVolume = 0.7f;

    [Header("Tiempos")]
    [SerializeField] float raiseDuration = 0.55f;
    [SerializeField] float lowerDuration = 0.35f;
    [SerializeField] float minReadTime = 0.5f;
    [SerializeField] float itemSpinSpeed = 45f;

    public bool IsPlaying { get; private set; }

    AudioSource source;

    // En 2D y sin reverb, al revés que las pisadas y la alarma. No es un sonido que
    // ocurra en el planeta: es la respuesta del juego a lo que acabas de hacer, y
    // situarlo en el espacio lo alejaría justo cuando tiene que sentirse inmediato.
    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
    }

    public void Play(Collectible item)
    {
        if (IsPlaying || item == null || item.Entry == null) return;
        StartCoroutine(Sequence(item));
    }

    IEnumerator Sequence(Collectible item)
    {
        IsPlaying = true;

        if (source != null && pickupClip != null)
            source.PlayOneShot(pickupClip, pickupVolume * GameSettings.EffectsVolume);

        // El mundo se congela mientras dura la recogida.
        //
        // Sin esto la escena era una trampa: el control está quitado, la cámara bloqueada
        // y la carta espera indefinidamente a que pulses E — y mientras tanto las arañas
        // seguían caminando y podían alcanzarte. Morir sin poder hacer nada, leyendo un
        // texto que el propio juego te obligó a abrir, es de lo más injusto que puede
        // pasar. Y no era tensión aprovechable: no había ninguna decisión que tomar.
        //
        // Se congela justo antes de la ESPERA, no al empezar, y el motivo es concreto:
        // el animador del personaje adelanta sus poses a 12 fps leyendo Time.time, que se
        // detiene con el reloj. Parando el mundo desde el primer fotograma, el muñeco se
        // quedaría en la pose que tuviera y nunca llegaría a levantar el objeto.
        //
        // Subir y bajar duran nueve décimas entre las dos y son animaciones cerradas; lo
        // que no tenía fin era la lectura de la carta. Ahí es donde estaba el peligro.
        float relojPrevio = Time.timeScale;

        item.SuspendIdleMotion();
        foreach (var col in item.GetComponentsInChildren<Collider>()) col.enabled = false;

        if (playerController != null) playerController.enabled = false;
        if (playerCamera != null) playerCamera.InputLocked = true;
        if (playerAnimator != null) playerAnimator.HoldingUp = true;

        Vector3 startPos = item.transform.position;
        Quaternion startRot = item.transform.rotation;

        // Subida: el objeto va del suelo a las manos mientras la cámara entra al encuadre.
        float t = 0f;
        while (t < raiseDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / raiseDuration);

            item.transform.position = Vector3.Lerp(startPos, HoldPosition(), k);
            item.transform.rotation = Quaternion.Slerp(startRot, Quaternion.identity, k);
            ApplyCinematicPose(k);
            yield return null;
        }

        card.Show(item.Entry);

        // Mundo parado durante la lectura. La carta no tiene límite de tiempo, así que
        // sin esto una araña podía llegar y matarte mientras leías, sin control y con la
        // cámara bloqueada. No era tensión aprovechable: no había decisión que tomar.
        Time.timeScale = 0f;

        // Lectura: el objeto gira despacio hasta que confirmes.
        float held = 0f;
        while (held < minReadTime || !ConfirmPressed())
        {
            held += Time.unscaledDeltaTime;
            item.transform.position = HoldPosition();
            item.transform.Rotate(Vector3.up, itemSpinSpeed * Time.unscaledDeltaTime, Space.World);
            ApplyCinematicPose(1f);
            yield return null;
        }

        card.Hide();

        // El reloj vuelve en cuanto confirmas, antes de la bajada, por lo mismo que se
        // congelaba tarde: con el mundo parado el animador tampoco avanzaría durante la
        // bajada y el personaje se quedaría con el objeto en alto mientras la pieza baja
        // sola. Se devuelve al valor que había, no a uno fijo: si la pausa estuviera
        // abierta por encima, poner uno la descongelaría por debajo.
        Time.timeScale = relojPrevio;

        // Salida: la cámara vuelve a su órbita normal y el objeto se guarda.
        if (playerAnimator != null) playerAnimator.HoldingUp = false;
        Vector3 exitPos = item.transform.position;

        t = 0f;
        while (t < lowerDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.SmoothStep(0f, 1f, t / lowerDuration);
            item.transform.position = Vector3.Lerp(player.position + Vector3.up * 1.1f, exitPos, k);
            item.transform.localScale = Vector3.one * k;
            ApplyCinematicPose(k);
            yield return null;
        }

        if (playerCamera != null)
        {
            playerCamera.CinematicBlend = 0f;
            playerCamera.InputLocked = false;
        }
        if (playerController != null) playerController.enabled = true;

        Inventory.Instance?.Add(item.Entry);
        Destroy(item.gameObject);

        IsPlaying = false;
    }

    // Red de seguridad: si la escena cambia o el objeto muere con la secuencia a medias,
    // el mundo se quedaría congelado para siempre y el juego parecería colgado.
    void OnDisable()
    {
        if (IsPlaying) Time.timeScale = 1f;
    }

    static bool ConfirmPressed()
    {
        return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
    }

    Vector3 HoldPosition()
    {
        return player.position + Vector3.up * holdHeight + player.forward * 0.12f;
    }

    void ApplyCinematicPose(float blend)
    {
        if (playerCamera == null) return;

        Vector3 focus = HoldPosition();
        Vector3 camPos = player.position
                         + player.forward * cameraDistance
                         + player.right * cameraSideOffset
                         + Vector3.up * cameraHeight;

        playerCamera.CinematicPosition = camPos;
        playerCamera.CinematicRotation = Quaternion.LookRotation(focus - camPos);
        playerCamera.CinematicBlend = Mathf.Clamp01(blend);
    }
}
