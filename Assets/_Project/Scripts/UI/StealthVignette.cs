using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Oscurece los bordes de la pantalla al agacharse, al estilo Metal Gear Solid:
// avisa que estás en sigilo sin necesidad de ningún cartel.
public class StealthVignette : MonoBehaviour
{
    [SerializeField] PlayerController playerController;
    [SerializeField] Volume volume;

    [SerializeField] float normalIntensity = 0.42f;
    [SerializeField] float crouchIntensity = 0.70f;
    [SerializeField] float normalSmoothness = 0.6f;
    [SerializeField] float crouchSmoothness = 0.35f;
    [SerializeField] float blendSpeed = 4f;

    Vignette vignette;

    void Start()
    {
        if (volume == null || playerController == null) return;

        // volume.profile devuelve la copia en runtime: el asset del proyecto no se toca.
        if (!volume.profile.TryGet(out vignette))
        {
            Debug.LogWarning("StealthVignette no encontró un Vignette en el perfil de post-procesado.");
            return;
        }

        vignette.intensity.overrideState = true;
        vignette.smoothness.overrideState = true;
    }

    void Update()
    {
        if (vignette == null) return;

        bool crouching = playerController.IsCrouching;
        float targetIntensity = crouching ? crouchIntensity : normalIntensity;
        float targetSmoothness = crouching ? crouchSmoothness : normalSmoothness;

        vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, targetIntensity, Time.deltaTime * blendSpeed);
        vignette.smoothness.value = Mathf.Lerp(vignette.smoothness.value, targetSmoothness, Time.deltaTime * blendSpeed);
    }
}
