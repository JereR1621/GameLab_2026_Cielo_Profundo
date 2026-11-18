using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menú principal. El fondo no es una imagen: es una escena 3D de verdad, con el mismo
// cielo, la misma niebla y el mismo shader PS1 que el resto del juego. Un cuerpo
// enorme girando despacio, sin nada más.
//
// Se hace así porque es más barato que dibujar un fondo y porque promete exactamente
// lo que el juego entrega: lo primero que ves es lo que vas a estar mirando.
public static class MainMenuSceneBuilder
{
    [MenuItem("Cielo Profundo/Construir menú principal")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        camGO.tag = "MainCamera";

        // Sin esto no suena nada, por más que el AudioSource esté reproduciendo. La nave
        // y el planeta lo traen porque se lo pone PlayerRigFactory; esta cámara la armo a
        // mano, así que hay que ponerlo acá.
        camGO.AddComponent<AudioListener>();
        cam.fieldOfView = 42f;
        camGO.transform.position = new Vector3(0f, 1.4f, -14f);
        camGO.transform.rotation = Quaternion.Euler(4f, 0f, 0f);

        EnvironmentDressingFactory.EnableAdditionalLights();
        EnvironmentDressingFactory.AddPostProcessing();

        // Cielo propio del menú, en su propio material. NO se reutiliza el PS1_Sky de
        // las otras escenas: ese es un asset compartido, y repintarlo acá le cambiaría
        // el cielo a la nave y a todos los planetas.
        BuildMenuSky(cam);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.014f;
        RenderSettings.fogColor = new Color(0.09f, 0.05f, 0.16f);

        var keyLight = new GameObject("Luz").AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.transform.rotation = Quaternion.Euler(12f, 36f, 0f);
        keyLight.intensity = 1.5f;
        keyLight.color = new Color(1f, 0.86f, 0.74f);

        BuildBody();
        BuildDust();
        BuildDistantShip();

        var menuGO = new GameObject("MainMenu");
        var menu = menuGO.AddComponent<MainMenuUI>();
        var fade = menuGO.AddComponent<ScreenFade>();
        var launch = menuGO.AddComponent<MenuLaunchSequence>();

        var music = menuGO.AddComponent<AmbientMusic>();
        var musicSO = new SerializedObject(music);
        musicSO.FindProperty("clip").objectReferenceValue = PrepareMusic();
        musicSO.ApplyModifiedPropertiesWithoutUndo();

        // Los grupos que se desvanecen los crea MainMenuUI en su Awake, así que no
        // existen todavía y no se pueden cablear uno por uno desde acá. Se pasa el menú
        // entero y la secuencia los pide al arrancar.
        var launchSO = new SerializedObject(launch);
        launchSO.FindProperty("cameraTransform").objectReferenceValue = camGO.transform;
        launchSO.FindProperty("fade").objectReferenceValue = fade;
        launchSO.FindProperty("menu").objectReferenceValue = menu;
        launchSO.FindProperty("music").objectReferenceValue = music;
        launchSO.ApplyModifiedPropertiesWithoutUndo();

        var menuSO = new SerializedObject(menu);
        menuSO.FindProperty("launch").objectReferenceValue = launch;
        menuSO.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory("Assets/_Project/Scenes");
        string scenePath = "Assets/_Project/Scenes/Main_Menu.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        // Primera del build: es por donde entra el jugador.
        var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        buildScenes.RemoveAll(s => s.path == scenePath);
        buildScenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = buildScenes.ToArray();

        Debug.Log("Menú principal construido en " + scenePath);
    }

    // El WAV pesa 17 MB. Sin tocar la importación se iría entero al build y además se
    // cargaría completo en memoria antes de sonar.
    //
    // Streaming + Vorbis lo deja en algo más de un mega y lo lee del disco a medida que
    // suena, que es exactamente lo que se quiere de un tema largo de fondo. En estéreo,
    // sin forzar a mono: es música, no un efecto posicional.
    static AudioClip PrepareMusic()
    {
        const string ruta = "Assets/_Project/Audio/Musica/MusicaMenu.wav";

        var importer = AssetImporter.GetAtPath(ruta) as AudioImporter;
        if (importer == null)
        {
            Debug.LogWarning("MainMenuSceneBuilder: no encontré la música en " + ruta);
            return null;
        }

        var ajustes = importer.defaultSampleSettings;
        ajustes.loadType = AudioClipLoadType.Streaming;
        ajustes.compressionFormat = AudioCompressionFormat.Vorbis;
        ajustes.quality = 0.6f;

        // La precarga vive dentro de los ajustes por plataforma, no en el importador:
        // el campo suelto quedó obsoleto.
        ajustes.preloadAudioData = false;

        importer.defaultSampleSettings = ajustes;
        importer.forceToMono = false;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
    }

    // Violeta profundo y más estrellas que en cualquier cuerpo de la ruta: el menú no
    // está en ningún destino en particular, está en el medio.
    static void BuildMenuSky(Camera cam)
    {
        Directory.CreateDirectory("Assets/_Project/Materials");
        AssetDatabase.Refresh();

        const string path = "Assets/_Project/Materials/PS1_MenuSky.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Cielo Profundo/PS1 Sky"));
            AssetDatabase.CreateAsset(mat, path);
        }

        // Se reescribe siempre, no solo al crearlo: si no, tocar estos valores acá no
        // tendría efecto sobre un material que ya existe.
        mat.SetColor("_ColorTop", new Color(0.028f, 0.018f, 0.068f));
        mat.SetColor("_ColorHorizon", new Color(0.17f, 0.08f, 0.30f));
        mat.SetFloat("_StarDensity", 260f);
        mat.SetColor("_SunColor", new Color(0.92f, 0.80f, 1f));
        mat.SetFloat("_HaloStrength", 0.55f);
        EditorUtility.SetDirty(mat);

        RenderSettings.skybox = mat;
        if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
    }

    // El cuerpo del fondo: una esfera facetada enorme, corrida a la derecha y abajo
    // para que el título respire y ella ocupe el lado vacío del encuadre.
    static void BuildBody()
    {
        var mat = MenuMaterial("PS1_MenuBody", new Color(0.40f, 0.26f, 0.46f));

        var go = new GameObject("CuerpoFondo");
        go.transform.position = new Vector3(9.5f, -3.5f, 16f);
        go.transform.rotation = Quaternion.Euler(-14f, 0f, 8f);

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = LowPolyMeshFactory.CreateFacetedSphere(18, 12, 11f);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;

        // Giro lentísimo. Reutiliza el mismo componente que flota los objetos y los
        // signos de la nave: sin vaivén, solo rotación.
        var spin = go.AddComponent<BobSpin>();
        spin.Configure(1.4f, 0f, 0f);

        // Un satélite chico, para dar escala. Sin él la esfera podría ser una pelota.
        var moonMat = MenuMaterial("PS1_MenuMoon", new Color(0.62f, 0.58f, 0.70f));
        var moon = new GameObject("Satelite");
        moon.transform.position = new Vector3(-7.5f, 4.2f, 22f);
        var moonMF = moon.AddComponent<MeshFilter>();
        moonMF.sharedMesh = LowPolyMeshFactory.CreateFacetedSphere(10, 7, 1.5f);
        moon.AddComponent<MeshRenderer>().sharedMaterial = moonMat;

        var moonSpin = moon.AddComponent<BobSpin>();
        moonSpin.Configure(6f, 0.35f, 0.25f);
    }

    // Polvo suspendido cruzando el cuadro muy despacio. Es lo que evita que el fondo
    // parezca una imagen fija cuando el jugador se queda mirando el menú.
    static void BuildDust()
    {
        var mat = MenuMaterial("PS1_MenuDust", new Color(0.78f, 0.72f, 0.92f));

        var fieldGO = new GameObject("Polvo");
        fieldGO.transform.position = new Vector3(0f, 0f, 6f);

        var field = fieldGO.AddComponent<StarStreakField>();
        var so = new SerializedObject(field);
        so.FindProperty("streakMaterial").objectReferenceValue = mat;
        so.FindProperty("count").intValue = 40;
        so.FindProperty("spread").vector2Value = new Vector2(26f, 14f);
        so.FindProperty("distance").floatValue = 46f;
        so.FindProperty("speed").floatValue = 2.2f;
        so.FindProperty("streakLength").floatValue = 0.5f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // La nave, lejos y de frente. Nunca se llega hasta ella: la de verdad está armada
    // como diorama abierto por dos lados, así que de cerca se vería la casa de muñecas.
    // A esta distancia y con la niebla es una silueta con una luz que parpadea, y eso
    // es todo lo que hace falta para que sea un destino.
    static void BuildDistantShip()
    {
        var hull = MenuMaterial("PS1_MenuShip", new Color(0.28f, 0.30f, 0.38f));
        var trim = MenuMaterial("PS1_MenuShipTrim", new Color(0.16f, 0.17f, 0.22f));

        var root = new GameObject("NaveLejana");
        root.transform.position = new Vector3(1.6f, 2.6f, 70f);
        root.transform.rotation = Quaternion.Euler(0f, -152f, 6f);

        // Cerrada por todos lados, al revés que la del lobby.
        AddPart(root.transform, PrimitiveType.Cube, "Casco", new Vector3(0f, 0f, 0f), new Vector3(3.4f, 1.2f, 6.2f), hull);
        AddPart(root.transform, PrimitiveType.Cube, "Morro", new Vector3(0f, 0f, 3.6f), new Vector3(1.9f, 0.9f, 1.6f), hull);
        AddPart(root.transform, PrimitiveType.Cube, "Torreta", new Vector3(0f, 0.9f, -0.4f), new Vector3(2.1f, 0.8f, 2.6f), hull);
        AddPart(root.transform, PrimitiveType.Cube, "AlaIzq", new Vector3(-3.1f, -0.1f, -0.8f), new Vector3(3.2f, 0.3f, 2.4f), trim);
        AddPart(root.transform, PrimitiveType.Cube, "AlaDer", new Vector3(3.1f, -0.1f, -0.8f), new Vector3(3.2f, 0.3f, 2.4f), trim);
        AddPart(root.transform, PrimitiveType.Cube, "Motor", new Vector3(0f, -0.1f, -3.6f), new Vector3(2.6f, 1f, 1.4f), trim);

        // La luz de navegación es lo único que de verdad se ve de lejos, igual que la
        // baliza del módulo de ascenso. Es lo que convierte la mancha en una nave.
        var beaconGO = new GameObject("Baliza");
        beaconGO.transform.SetParent(root.transform, false);
        beaconGO.transform.localPosition = new Vector3(0f, 1.6f, -0.4f);

        var beacon = beaconGO.AddComponent<Light>();
        beacon.type = LightType.Point;
        beacon.color = new Color(1f, 0.52f, 0.36f);
        beacon.range = 30f;
        beacon.intensity = 9f;
        beaconGO.AddComponent<NavLightBlink>();

        var glowGO = new GameObject("Escape");
        glowGO.transform.SetParent(root.transform, false);
        glowGO.transform.localPosition = new Vector3(0f, -0.1f, -4.6f);

        var glow = glowGO.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.55f, 0.78f, 1f);
        glow.range = 22f;
        glow.intensity = 6f;
    }

    static void AddPart(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    static Material MenuMaterial(string assetName, Color color)
    {
        Directory.CreateDirectory("Assets/_Project/Materials");
        AssetDatabase.Refresh();

        string path = $"Assets/_Project/Materials/{assetName}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var mat = new Material(Shader.Find("Cielo Profundo/PS1 Vertex Snap"));
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_SnapResolution", 140f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
