using UnityEngine;

// Luces de navegación del casco. Parpadean a 12 fps como todo lo demás del juego:
// un pulso suave a 60 fps se leería de otro juego que el resto.
public class NavLightBlink : MonoBehaviour
{
    [SerializeField] Renderer target;
    [SerializeField] Light glow;
    [SerializeField] float period = 1.6f;
    [SerializeField] float onFraction = 0.35f;
    [SerializeField] float phase;
    [SerializeField] float frameRate = 12f;

    Material instance;
    Color baseColor;
    int lastFrame = -1;

    void Start()
    {
        if (target == null) return;

        // Instancia propia: si no, cambiar el color afectaría a todas las luces que
        // comparten el material.
        instance = target.material;
        baseColor = instance.GetColor("_BaseColor");
    }

    void Update()
    {
        int frame = Mathf.FloorToInt(Time.time * frameRate);
        if (frame == lastFrame) return;
        lastFrame = frame;

        float t = Mathf.Repeat((Time.time + phase) / period, 1f);
        bool on = t < onFraction;

        if (instance != null)
            instance.SetColor("_BaseColor", on ? baseColor : baseColor * 0.18f);

        if (glow != null)
            glow.intensity = on ? 2.4f : 0f;
    }
}
