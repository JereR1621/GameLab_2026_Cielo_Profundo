using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SandboxSceneBuilder
{
    [MenuItem("Cielo Profundo/Construir escena sandbox")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        ConfigurePipeline();
        ConfigureFog();

        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        light.intensity = 1.0f;

        var ps1Mat = BuildPs1Material();

        var rig = PlayerRigFactory.Create(new Vector3(0f, 1.5f, -8f));
        var playerController = rig.controller;
        var playerCam = rig.camera;

        EnvironmentDressingFactory.AddPostProcessing();
        EnvironmentDressingFactory.ApplySkybox(playerCam.GetComponent<Camera>());

        // Zona abierta: la superficie donde ya aterrizamos, exploración amplia sin plataformeo.
        var island = GameObject.CreatePrimitive(PrimitiveType.Cube);
        island.name = "OpenArea_Island";
        island.transform.position = new Vector3(0f, -0.5f, 0f);
        island.transform.localScale = new Vector3(34f, 1f, 34f);
        island.GetComponent<Renderer>().sharedMaterial = ps1Mat;

        // Sector de plataformeo: arranca donde termina la zona abierta.
        Vector3[] gauntlet =
        {
            new Vector3(4f, 0.5f, 22f),
            new Vector3(9f, 1.5f, 26f),
            new Vector3(9f, 1.5f, 32f),
            new Vector3(4f, 3f, 36f),
            new Vector3(-2f, 4f, 40f),
            new Vector3(-8f, 3f, 36f),
        };
        for (int i = 0; i < gauntlet.Length; i++)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Platform_" + i;
            box.transform.position = gauntlet[i];
            box.transform.localScale = new Vector3(3f, 0.5f, 3f);
            box.GetComponent<Renderer>().sharedMaterial = ps1Mat;
        }

        // Sistemas: inventario y su UI, con la referencia al jugador.
        PlayerRigFactory.CreateGameSystems(rig);

        // Fichas de catálogo: salen de Resources/items.json igual que en el planeta.
        // El sandbox usa las del primer tramo, que son las de rutina.
        var fichas = CatalogDatabase.EntriesForTramo(1);
        Vector3[] collectiblePositions =
        {
            new Vector3(6f, 0.6f, 4f),
            new Vector3(-9f, 0.6f, -3f),
            new Vector3(9f, 2.3f, 32f),
            new Vector3(-8f, 3.8f, 36f),
        };

        int placed = Mathf.Min(fichas.Length, collectiblePositions.Length);
        for (int i = 0; i < placed; i++)
        {
            var accent = new Material(Shader.Find("Cielo Profundo/PS1 Vertex Snap"));
            accent.SetColor("_BaseColor", fichas[i].accentColor);
            accent.SetFloat("_SnapResolution", 140f);
            ItemVisualFactory.Spawn(fichas[i], collectiblePositions[i], null, ps1Mat, accent);
        }

        string scenePath = "Assets/_Project/Scenes/Sandbox_Controller.unity";
        Directory.CreateDirectory("Assets/_Project/Scenes");
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

        Debug.Log("Sandbox construido en " + scenePath);
    }

    static Material BuildPs1Material()
    {
        Directory.CreateDirectory("Assets/_Project/Materials");
        AssetDatabase.Refresh();

        string path = "Assets/_Project/Materials/PS1_Sandbox.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Cielo Profundo/PS1 Vertex Snap");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.435f, 0.788f, 0.831f));
        mat.SetFloat("_SnapResolution", 140f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void ConfigurePipeline()
    {
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null) return;
        urpAsset.renderScale = 0.5f;
        EditorUtility.SetDirty(urpAsset);
        AssetDatabase.SaveAssets();
    }

    static void ConfigureFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.043f, 0.024f, 0.125f);
        RenderSettings.fogDensity = 0.03f;
    }
}
