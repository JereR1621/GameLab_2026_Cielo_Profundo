using UnityEngine;

// Animación del fuego a 12 fps, la misma cadencia que el personaje. Si el fuego
// fuera suave a 60 fps se notaría que pertenece a otro juego que el resto.
public class FlameFlicker : MonoBehaviour
{
    [SerializeField] Transform[] tongues;
    [SerializeField] Light glow;
    [SerializeField] float frameRate = 12f;
    [SerializeField] float baseIntensity = 3.2f;

    float[] phases;
    int lastFrame = -1;

    public void Configure(Transform[] flameTongues, Light flameLight)
    {
        tongues = flameTongues;
        glow = flameLight;
        if (glow != null) baseIntensity = glow.intensity;
    }

    void Start()
    {
        if (tongues == null) return;
        phases = new float[tongues.Length];
        for (int i = 0; i < phases.Length; i++)
            phases[i] = Random.Range(0f, 10f);
    }

    void Update()
    {
        if (tongues == null || phases == null) return;

        int frame = Mathf.FloorToInt(Time.time * frameRate);
        if (frame == lastFrame) return;
        lastFrame = frame;

        float t = Time.time;

        for (int i = 0; i < tongues.Length; i++)
        {
            if (tongues[i] == null) continue;

            float wobble = Mathf.PerlinNoise(phases[i], t * 2.6f);
            float stretch = Mathf.Lerp(0.72f, 1.35f, wobble);
            float width = Mathf.Lerp(1.15f, 0.8f, wobble);

            tongues[i].localScale = new Vector3(width, stretch, width);
            tongues[i].localRotation = Quaternion.Euler(
                (wobble - 0.5f) * 14f,
                t * 40f + phases[i] * 60f,
                (Mathf.PerlinNoise(t * 2.2f, phases[i]) - 0.5f) * 16f);
        }

        if (glow != null)
        {
            float pulse = Mathf.PerlinNoise(t * 4.5f, 0.5f);
            glow.intensity = baseIntensity * Mathf.Lerp(0.65f, 1.3f, pulse);
        }
    }
}
