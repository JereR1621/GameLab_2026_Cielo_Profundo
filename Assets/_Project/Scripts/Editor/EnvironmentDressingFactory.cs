using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class EnvironmentDressingFactory
{
    // Reusa el override si ya está en el perfil; solo lo agrega al asset la primera vez.
    static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet<T>(out var existing)) return existing;

        var added = profile.Add<T>(true);
        AssetDatabase.AddObjectToAsset(added, profile);
        return added;
    }

    // Sin esto el shader puede soportar luces puntuales pero el pipeline no se las
    // manda, y bengalas y fuego no iluminan nada. Se toca por SerializedObject porque
    // estas propiedades no siempre tienen setter público según la versión de URP.
    public static void EnableAdditionalLights(int maxPerObject = 8)
    {
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null)
        {
            Debug.LogWarning("No hay un URP Asset activo: las luces puntuales no van a iluminar.");
            return;
        }

        var so = new SerializedObject(urpAsset);
        var mode = so.FindProperty("m_AdditionalLightsRenderingMode");
        if (mode != null) mode.intValue = (int)LightRenderingMode.PerPixel;

        var perObject = so.FindProperty("m_AdditionalLightsPerObjectLimit");
        if (perObject != null) perObject.intValue = Mathf.Clamp(maxPerObject, 1, 8);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(urpAsset);
        AssetDatabase.SaveAssets();
    }

    public static Volume AddPostProcessing()
    {
        Directory.CreateDirectory("Assets/_Project/Data/PostProcessing");
        AssetDatabase.Refresh();

        string path = "Assets/_Project/Data/PostProcessing/PS1_Volume.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        // Los valores se reescriben en cada construcción, no solo al crear el asset.
        // Si no, cambiar un número acá no tendría ningún efecto sobre un perfil ya creado.
        var bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.overrideState = true; bloom.threshold.value = 1.05f;
        bloom.intensity.overrideState = true; bloom.intensity.value = 0.55f;
        bloom.scatter.overrideState = true; bloom.scatter.value = 0.7f;

        var vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.overrideState = true; vignette.intensity.value = 0.42f;
        vignette.smoothness.overrideState = true; vignette.smoothness.value = 0.55f;
        vignette.color.overrideState = true; vignette.color.value = new Color(0.01f, 0.005f, 0.03f);

        var chroma = GetOrAdd<ChromaticAberration>(profile);
        chroma.intensity.overrideState = true; chroma.intensity.value = 0.2f;

        var grain = GetOrAdd<FilmGrain>(profile);
        grain.intensity.overrideState = true; grain.intensity.value = 0.34f;
        grain.response.overrideState = true; grain.response.value = 0.65f;

        // Sin tonemapping, todo lo que pasa de 1 se corta en blanco duro. Eso obligaba a
        // dejar la linterna floja: cualquier intensidad suficiente para el cuerpo más
        // oscuro de la ruta quemaba el suelo de los pies en todos los demás. Con esto las
        // luces fuertes hacen degradado en vez de cortarse, y la bengala deja de clipear.
        //
        // Neutral y no ACES a propósito: ACES tuerce bastante el color, y todo el juego
        // está construido sobre una paleta distinta por cuerpo que conviene no mover.
        var tonemap = GetOrAdd<Tonemapping>(profile);
        tonemap.mode.overrideState = true; tonemap.mode.value = TonemappingMode.Neutral;

        var colorAdj = GetOrAdd<ColorAdjustments>(profile);
        colorAdj.postExposure.overrideState = true; colorAdj.postExposure.value = -0.35f;
        colorAdj.contrast.overrideState = true; colorAdj.contrast.value = 18f;
        colorAdj.saturation.overrideState = true; colorAdj.saturation.value = -10f;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var volumeGO = new GameObject("GlobalVolume");
        var volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.weight = 1f;
        // sharedProfile es el campo serializado: es el que hay que asignar al construir
        // la escena. 'profile' crearía una copia en memoria que no se guardaría.
        volume.sharedProfile = profile;
        return volume;
    }

    public static Material ApplySkybox(Camera cam)
    {
        Directory.CreateDirectory("Assets/_Project/Materials");
        AssetDatabase.Refresh();

        string path = "Assets/_Project/Materials/PS1_Sky.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Cielo Profundo/PS1 Sky");
            mat = new Material(shader);
            mat.SetColor("_ColorTop", new Color(0.043f, 0.024f, 0.125f));
            mat.SetColor("_ColorHorizon", new Color(0.165f, 0.086f, 0.4f));
            AssetDatabase.CreateAsset(mat, path);
        }

        RenderSettings.skybox = mat;
        if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
        return mat;
    }
}
