using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlanetAtmosphere : MonoBehaviour
{
    [SerializeField] NoiseTerrainGenerator generator;
    [SerializeField] Volume volume;
    [SerializeField] Material skyTemplate;

    [Header("Cielo: se mantiene oscuro arriba y más encendido en el horizonte")]
    [SerializeField, Range(0f, 1f)] float skyTopSaturation = 0.8f;
    [SerializeField, Range(0f, 1f)] float skyTopValue = 0.055f;
    [SerializeField, Range(0f, 1f)] float skyHorizonSaturation = 0.75f;
    [SerializeField, Range(0f, 1f)] float skyHorizonValue = 0.21f;

    [Header("Filtro de cámara: leve a propósito, para teñir sin repintar el nivel")]
    [SerializeField, Range(0f, 0.4f)] float filterStrength = 0.13f;

    [Header("Bruma")]
    [SerializeField] float fogDensityMin = 0.078f;
    [SerializeField] float fogDensityMax = 0.115f;

    [Header("Estrella del sistema")]
    [SerializeField] Light sunLight;
    [SerializeField] float sunElevationMin = 12f;
    [SerializeField] float sunElevationMax = 38f;

    Material skyInstance;

    void OnEnable()
    {
        if (generator != null) generator.OnTerrainReady += Apply;
    }

    void OnDisable()
    {
        if (generator != null) generator.OnTerrainReady -= Apply;
    }

    [Header("Cierre del catálogo")]
    [Tooltip("A qué fracción baja la bruma cuando se levanta la última pieza. 0.45 = se ve algo más del doble de lejos.")]
    [SerializeField, Range(0.1f, 1f)] float fogOnCatalogueComplete = 0.45f;

    [Tooltip("Segundos que tarda en despejarse. Lento: de golpe se vería como un fallo gráfico, y despacio se lee como que el aire cambia.")]
    [SerializeField] float clearSeconds = 5f;

    float fogBase = -1f;

    // La bruma se abre al cerrar el catálogo.
    //
    // No es solo una ayuda para encontrar el módulo, aunque también: es la señal de que
    // el planeta cambió de estado. El mismo sitio que llevabas veinte minutos recorriendo
    // a ciegas de pronto se ve, y lo primero que ves es la columna de la baliza al fondo.
    //
    // Y tiene su precio, que es lo que la hace algo más que un regalo: si tú ves más
    // lejos, las arañas también se ven desde más lejos. El tramo de vuelta es el único
    // del juego donde las miras venir.
    void Update()
    {
        if (Inventory.Instance == null || !Inventory.Instance.IsComplete) return;

        // La densidad de partida se toma la primera vez que hace falta, no al arrancar:
        // este componente corre antes de que el generador de terreno la haya puesto.
        if (fogBase < 0f) fogBase = RenderSettings.fogDensity;

        float objetivo = fogBase * fogOnCatalogueComplete;
        if (Mathf.Approximately(RenderSettings.fogDensity, objetivo)) return;

        float paso = Mathf.Max(0.0001f, (fogBase - objetivo) / Mathf.Max(0.1f, clearSeconds));
        RenderSettings.fogDensity = Mathf.MoveTowards(RenderSettings.fogDensity, objetivo,
                                                      paso * Time.deltaTime);
    }

    void Apply(Vector3 spawnPoint)
    {
        var rng = new System.Random(generator.EffectiveSeed);
        float baseHue = (float)rng.NextDouble();

        // La orientación de la estrella se sigue sorteando: es lo único que no hace
        // falta fijar por cuerpo, y da variedad sin cambiar la identidad del lugar.
        float sunAzimuth = (float)rng.NextDouble() * 360f;
        float sunElevation = Mathf.Lerp(sunElevationMin, sunElevationMax, (float)rng.NextDouble());
        Vector3 sunDirection = Quaternion.Euler(-sunElevation, sunAzimuth, 0f) * Vector3.forward;

        var body = PlanetSession.Instance != null ? PlanetSession.Instance.Body : null;

        if (body != null && !string.IsNullOrEmpty(body.cieloAlto))
        {
            // Colores y densidades definidos en ruta.json: cada cuerpo es siempre
            // el mismo lugar, no un sorteo distinto en cada visita.
            Color top = body.ParseColor(body.cieloAlto, Color.black);
            Color horizon = body.ParseColor(body.cieloHorizonte, Color.black);
            Color starColor = body.ParseColor(body.colorEstrella, Color.white);

            ApplySkyExplicit(top, horizon, starColor, sunDirection, body.nieblaDensidad);
            ApplySunLightExplicit(sunDirection, starColor, body.luzIntensidad);
            ApplyCameraFilter(baseHue);
            return;
        }

        // Sin datos de ruta (por ejemplo en pruebas sueltas) se vuelve al sorteo.
        float fogDensity = Mathf.Lerp(fogDensityMin, fogDensityMax, (float)rng.NextDouble());
        ApplySky(baseHue, sunDirection, fogDensity);
        ApplyCameraFilter(baseHue);
        ApplySunLight(sunDirection, baseHue);
    }

    void ApplySkyExplicit(Color top, Color horizon, Color starColor, Vector3 sunDirection, float fogDensity)
    {
        if (skyTemplate == null) return;

        if (skyInstance == null) skyInstance = new Material(skyTemplate);
        skyInstance.SetColor("_ColorTop", top);
        skyInstance.SetColor("_ColorHorizon", horizon);
        skyInstance.SetVector("_SunDir", sunDirection);
        skyInstance.SetColor("_SunColor", starColor);
        RenderSettings.skybox = skyInstance;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = Color.Lerp(top, horizon, 0.45f);
        RenderSettings.fogDensity = fogDensity;
    }

    void ApplySunLightExplicit(Vector3 sunDirection, Color starColor, float intensity)
    {
        if (sunLight == null) return;
        sunLight.transform.rotation = Quaternion.LookRotation(-sunDirection);
        sunLight.color = starColor;
        sunLight.intensity = Mathf.Max(0.05f, intensity);
    }

    void ApplySky(float baseHue, Vector3 sunDirection, float fogDensity)
    {
        if (skyTemplate == null) return;

        Color top = Color.HSVToRGB(baseHue, skyTopSaturation, skyTopValue);
        Color horizon = Color.HSVToRGB(Mathf.Repeat(baseHue + 0.08f, 1f), skyHorizonSaturation, skyHorizonValue);
        Color sunColor = Color.HSVToRGB(Mathf.Repeat(baseHue + 0.12f, 1f), 0.35f, 1f);

        // Instancia propia: nunca escribimos sobre el material del proyecto.
        if (skyInstance == null) skyInstance = new Material(skyTemplate);
        skyInstance.SetColor("_ColorTop", top);
        skyInstance.SetColor("_ColorHorizon", horizon);
        skyInstance.SetVector("_SunDir", sunDirection);
        skyInstance.SetColor("_SunColor", sunColor);
        RenderSettings.skybox = skyInstance;

        // La niebla sigue al cielo, si no el horizonte se corta con una línea dura.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = Color.Lerp(top, horizon, 0.45f);
        RenderSettings.fogDensity = fogDensity;
    }

    // La luz direccional apunta desde donde está la estrella visible: si no coinciden,
    // las sombras del terreno contradicen lo que se ve en el cielo.
    void ApplySunLight(Vector3 sunDirection, float baseHue)
    {
        if (sunLight == null) return;
        sunLight.transform.rotation = Quaternion.LookRotation(-sunDirection);
        sunLight.color = Color.HSVToRGB(Mathf.Repeat(baseHue + 0.1f, 1f), 0.18f, 1f);
    }

    void ApplyCameraFilter(float baseHue)
    {
        if (volume == null) return;

        // volume.profile devuelve una copia en runtime (a diferencia de sharedProfile,
        // que es el asset del proyecto y quedaría modificado de forma permanente).
        if (!volume.profile.TryGet<ColorAdjustments>(out var colorAdjustments)) return;

        float filterHue = Mathf.Repeat(baseHue + 0.5f, 1f);
        colorAdjustments.colorFilter.overrideState = true;
        colorAdjustments.colorFilter.value = Color.HSVToRGB(filterHue, filterStrength, 1f);
    }
}
