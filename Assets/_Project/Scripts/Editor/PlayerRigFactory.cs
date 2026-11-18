using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class PlayerRigFactory
{
    public struct Rig
    {
        public GameObject player;
        public PlayerController controller;
        public PlayerCamera camera;
    }

    public static Rig Create(Vector3 spawnPos)
    {
        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = spawnPos;
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 1f, 0f);
        // Alto a propósito: el terreno con cerros tiene escalones entre celdas vecinas
        // y con el valor por defecto (0.3) el jugador se quedaría trabado al subir.
        cc.stepOffset = 0.85f;

        BuildPlayerVisual(player.transform);

        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        camGO.tag = "MainCamera";
        camGO.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.043f, 0.024f, 0.125f);
        var camData = camGO.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        var playerCam = camGO.AddComponent<PlayerCamera>();

        var controller = player.AddComponent<PlayerController>();
        player.AddComponent<RespawnOnFall>();

        // Parpadeo mientras dura la invulnerabilidad. Busca el objeto "Visual" solo, así
        // que no hay nada que cablear.
        player.AddComponent<InvulnerabilityBlink>();
        var animator = player.AddComponent<PlayerAnimator>();

        // Pisadas. Van en el propio jugador y no en un objeto suelto porque se enganchan
        // al animador, que es quien sabe cuándo se apoya cada pie.
        var pasos = player.AddComponent<PlayerFootsteps>();
        var pasosSO = new SerializedObject(pasos);
        pasosSO.FindProperty("playerAnimator").objectReferenceValue = animator;
        pasosSO.FindProperty("left").objectReferenceValue = PrepararEfecto("paso_izq", true);
        pasosSO.FindProperty("right").objectReferenceValue = PrepararEfecto("paso_der", true);
        pasosSO.ApplyModifiedPropertiesWithoutUndo();

        var controllerSO = new SerializedObject(controller);
        controllerSO.FindProperty("cameraPivot").objectReferenceValue = camGO.transform;
        controllerSO.ApplyModifiedPropertiesWithoutUndo();

        var cameraSO = new SerializedObject(playerCam);
        cameraSO.FindProperty("target").objectReferenceValue = player.transform;
        cameraSO.ApplyModifiedPropertiesWithoutUndo();

        var animatorSO = new SerializedObject(animator);
        animatorSO.FindProperty("playerController").objectReferenceValue = controller;
        animatorSO.ApplyModifiedPropertiesWithoutUndo();

        return new Rig { player = player, controller = controller, camera = playerCam };
    }

    public static InventoryUI CreateGameSystems(Rig rig)
    {
        var systemsGO = new GameObject("GameSystems");
        systemsGO.AddComponent<Inventory>();

        var invUI = systemsGO.AddComponent<InventoryUI>();
        var invSO = new SerializedObject(invUI);
        invSO.FindProperty("playerController").objectReferenceValue = rig.controller;
        invSO.FindProperty("playerCamera").objectReferenceValue = rig.camera;
        invSO.ApplyModifiedPropertiesWithoutUndo();

        var card = systemsGO.AddComponent<ItemCardUI>();
        var prompt = systemsGO.AddComponent<InteractionPromptUI>();

        var sequence = systemsGO.AddComponent<ItemPickupSequence>();
        var seqSO = new SerializedObject(sequence);
        seqSO.FindProperty("player").objectReferenceValue = rig.player.transform;
        seqSO.FindProperty("playerController").objectReferenceValue = rig.controller;
        seqSO.FindProperty("playerCamera").objectReferenceValue = rig.camera;
        seqSO.FindProperty("playerAnimator").objectReferenceValue = rig.player.GetComponent<PlayerAnimator>();
        seqSO.FindProperty("card").objectReferenceValue = card;
        seqSO.FindProperty("pickupClip").objectReferenceValue = PrepararEfecto("recoger", false);
        seqSO.ApplyModifiedPropertiesWithoutUndo();

        // Vida: corazones, y pantalla final al quedarse sin ninguno.
        systemsGO.AddComponent<PlayerHealth>();
        systemsGO.AddComponent<HeartsUI>();
        systemsGO.AddComponent<GameOverUI>();

        // Lanzabengalas: apuntado en primera persona con el marco del casco.
        var visorUI = systemsGO.AddComponent<HelmetVisorUI>();
        var launcher = rig.player.AddComponent<FlareLauncher>();
        var launcherSO = new SerializedObject(launcher);
        launcherSO.FindProperty("playerController").objectReferenceValue = rig.controller;
        launcherSO.FindProperty("playerCamera").objectReferenceValue = rig.camera;
        launcherSO.FindProperty("flareMaterial").objectReferenceValue = FlareMaterial();
        launcherSO.FindProperty("visorOverlay").objectReferenceValue = visorUI;
        launcherSO.ApplyModifiedPropertiesWithoutUndo();

        var ammoUI = systemsGO.AddComponent<FlareAmmoUI>();
        var ammoSO = new SerializedObject(ammoUI);
        ammoSO.FindProperty("launcher").objectReferenceValue = launcher;
        ammoSO.ApplyModifiedPropertiesWithoutUndo();

        // Barra de estamina para correr.
        var staminaBar = systemsGO.AddComponent<StaminaBarUI>();
        var staminaSO = new SerializedObject(staminaBar);
        staminaSO.FindProperty("playerController").objectReferenceValue = rig.controller;
        staminaSO.ApplyModifiedPropertiesWithoutUndo();

        // Avisos de detección y de pérdida de rastro.
        var reactions = systemsGO.AddComponent<SpiderReactionSequence>();
        var reactionsSO = new SerializedObject(reactions);
        reactionsSO.FindProperty("player").objectReferenceValue = rig.player.transform;
        reactionsSO.FindProperty("playerController").objectReferenceValue = rig.controller;
        reactionsSO.FindProperty("playerCamera").objectReferenceValue = rig.camera;
        reactionsSO.ApplyModifiedPropertiesWithoutUndo();

        // Forcejeo cuando la araña te atrapa.
        var struggleBar = systemsGO.AddComponent<StruggleBarUI>();
        var catchSequence = systemsGO.AddComponent<SpiderCatchSequence>();
        var catchSO = new SerializedObject(catchSequence);
        catchSO.FindProperty("player").objectReferenceValue = rig.player.transform;
        catchSO.FindProperty("playerController").objectReferenceValue = rig.controller;
        catchSO.FindProperty("playerCamera").objectReferenceValue = rig.camera;
        catchSO.FindProperty("playerAnimator").objectReferenceValue = rig.player.GetComponent<PlayerAnimator>();
        catchSO.FindProperty("bar").objectReferenceValue = struggleBar;
        catchSO.FindProperty("catchClip").objectReferenceValue = PrepararEfecto("captura", false);
        catchSO.ApplyModifiedPropertiesWithoutUndo();

        var interaction = rig.player.AddComponent<PlayerInteraction>();
        var interactionSO = new SerializedObject(interaction);
        interactionSO.FindProperty("pickupSequence").objectReferenceValue = sequence;
        interactionSO.FindProperty("prompt").objectReferenceValue = prompt;
        interactionSO.ApplyModifiedPropertiesWithoutUndo();

        return invUI;
    }

    // Silueta facetada armada con formas simples: sin cara, sin curvas suaves.
    // El CharacterController sigue siendo la única cápsula de colisión real;
    // esto es puramente visual, un hijo del jugador.
    //
    // Brazos y piernas cuelgan de un pivote vacío puesto en la articulación
    // (hombro / cadera). Sin eso, rotar un miembro lo haría girar sobre su propio
    // centro en vez de sobre la articulación, y la caminata se vería rota.
    // Cadena de articulaciones encadenadas: cadera → muslo → rodilla → pantorrilla →
    // tobillo → pie, y hombro → brazo → codo → antebrazo. Con un solo hueso por
    // miembro el personaje no puede doblarse en ningún lado y se ve acartonado.
    const float HipHeight = 0.88f;
    const float ThighLength = 0.42f;
    const float ShinLength = 0.40f;
    const float UpperArmLength = 0.34f;
    const float ForearmLength = 0.32f;

    // Traje presurizado. Lo que lo hace leer como astronauta y no como muñeco son
    // cuatro cosas: el casco con visor, la mochila, los anillos en las articulaciones
    // y el salto de grosor en guantes y botas.
    // Maniquí para la vitrina de la pantalla de controles: el mismo cuerpo del jugador,
    // sin control ni cámara ni física.
    //
    // Lo arma el constructor de escena y no la propia pantalla porque este archivo es de
    // editor —usa AssetDatabase para los materiales del traje— y no existe en tiempo de
    // ejecución. Así que el maniquí queda guardado en la escena, apagado, y la pantalla
    // solo lo enciende. Apagado no cuesta nada: ni se dibuja ni se le calcula la pose.
    //
    // El CharacterController va desactivado a propósito. PlayerAnimator lo exige por
    // atributo, pero en modo vitrina no se le lee ni la velocidad ni si toca suelo, y
    // activo sería una cápsula de colisión suelta a seiscientas unidades bajo el mapa.
    public static GameObject CreateMannequin(Vector3 pos, int layer)
    {
        var go = new GameObject("ManiquiControles");
        go.transform.position = pos;

        var cc = go.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 1f, 0f);
        cc.enabled = false;

        BuildPlayerVisual(go.transform);

        var anim = go.AddComponent<PlayerAnimator>();
        var animSO = new SerializedObject(anim);
        animSO.FindProperty("visualRoot").objectReferenceValue = go.transform.Find("Visual");
        animSO.ApplyModifiedPropertiesWithoutUndo();

        SetLayerRecursive(go, layer);
        go.SetActive(false);
        return go;
    }

    // La capa hay que ponerla en todo el árbol: el cuerpo son decenas de hijos y con la
    // capa solo en la raíz el maniquí sería invisible para la cámara de la vitrina.
    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
    }

    // Un efecto corto de pisada, importado como corresponde para un sonido que se dispara
    // muchas veces por segundo.
    //
    // Al revés que la música: sin comprimir y cargado entero en memoria. Un clip de dos
    // décimas ocupa nada, y descomprimirlo en cada paso —o peor, transmitirlo desde
    // disco— metería un retraso justo donde más se nota, porque el oído perdona un
    // sonido feo mucho antes que uno que llega tarde.
    static AudioClip PrepararEfecto(string nombre, bool mono)
    {
        string ruta = $"Assets/_Project/Audio/Efectos/{nombre}.wav";

        var importer = AssetImporter.GetAtPath(ruta) as AudioImporter;
        if (importer == null)
        {
            Debug.LogWarning("No encontré el efecto de pisada en " + ruta);
            return null;
        }

        var ajustes = importer.defaultSampleSettings;
        ajustes.loadType = AudioClipLoadType.DecompressOnLoad;
        ajustes.compressionFormat = AudioCompressionFormat.PCM;
        ajustes.preloadAudioData = true;

        importer.defaultSampleSettings = ajustes;
        importer.forceToMono = mono;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
    }

    static void BuildPlayerVisual(Transform parent)
    {
        var suit = SuitMaterial("PS1_Suit", new Color(0.80f, 0.80f, 0.78f));
        var dark = SuitMaterial("PS1_SuitJoint", new Color(0.20f, 0.20f, 0.24f));
        var visor = SuitMaterial("PS1_Visor", new Color(0.86f, 0.62f, 0.16f));
        var accent = SuitMaterial("PS1_SuitAccent", new Color(0.94f, 0.45f, 0.30f));

        var visualRoot = new GameObject("Visual");
        visualRoot.transform.SetParent(parent, false);

        var hips = AddJoint(visualRoot.transform, "Hips", new Vector3(0f, HipHeight, 0f));
        AddBox(hips, "Pelvis", new Vector3(0f, 0.02f, 0f), new Vector3(0.42f, 0.22f, 0.30f), suit);
        AddBox(hips, "Cinturon", new Vector3(0f, 0.11f, 0f), new Vector3(0.45f, 0.07f, 0.33f), dark);

        var spine = AddJoint(hips, "Spine", Vector3.zero);
        AddBox(spine, "Chest", new Vector3(0f, 0.28f, 0f), new Vector3(0.48f, 0.58f, 0.31f), suit);
        AddBox(spine, "Mochila", new Vector3(0f, 0.30f, -0.24f), new Vector3(0.40f, 0.50f, 0.20f), dark);
        AddBox(spine, "TanqueL", new Vector3(-0.11f, 0.32f, -0.33f), new Vector3(0.11f, 0.42f, 0.11f), suit);
        AddBox(spine, "TanqueR", new Vector3(0.11f, 0.32f, -0.33f), new Vector3(0.11f, 0.42f, 0.11f), suit);
        AddBox(spine, "PanelPecho", new Vector3(0f, 0.26f, 0.17f), new Vector3(0.24f, 0.17f, 0.05f), dark);
        AddBox(spine, "LuzPanel", new Vector3(-0.06f, 0.29f, 0.20f), new Vector3(0.05f, 0.04f, 0.02f), accent);

        var neck = AddJoint(spine, "Neck", new Vector3(0f, 0.56f, 0f));
        AddBox(neck, "AnilloCuello", new Vector3(0f, 0.02f, 0f), new Vector3(0.24f, 0.07f, 0.24f), dark);

        // Casco más grande que la cabeza, como corresponde a un traje presurizado.
        var helmet = AddMesh(neck, "Casco", LowPolyMeshFactory.CreateFacetedSphere(10, 7, 0.235f), suit);
        helmet.localPosition = new Vector3(0f, 0.22f, 0f);
        helmet.localScale = new Vector3(1f, 0.97f, 1.02f);

        // El visor es una banda horizontal achatada, no una esfera casi entera metida
        // adentro: así asoma parejo por el frente en vez de aparecer como manchas
        // doradas sueltas entre las facetas del casco.
        var glass = AddMesh(neck, "Visor", LowPolyMeshFactory.CreateFacetedSphere(10, 5, 0.21f), visor);
        glass.localPosition = new Vector3(0f, 0.225f, 0.055f);
        glass.localScale = new Vector3(0.92f, 0.60f, 0.95f);

        // Marco oscuro alrededor del visor: le da un borde y lo separa del casco.
        // Va a la misma profundidad que el visor pero más ancho y más alto, y asoma
        // un poco menos: así queda como un aro alrededor y no lo tapa.
        var frame = AddMesh(neck, "MarcoVisor", LowPolyMeshFactory.CreateFacetedSphere(10, 5, 0.222f), dark);
        frame.localPosition = new Vector3(0f, 0.225f, 0.055f);
        frame.localScale = new Vector3(0.98f, 0.76f, 0.88f);

        BuildArm(spine, "L", -1f, suit, dark, accent);
        BuildArm(spine, "R", 1f, suit, dark, accent);
        BuildLeg(hips, "L", -1f, suit, dark);
        BuildLeg(hips, "R", 1f, suit, dark);
    }

    static void BuildArm(Transform spine, string suffix, float side, Material suit, Material dark, Material accent)
    {
        var shoulder = AddJoint(spine, "Shoulder" + suffix, new Vector3(side * 0.29f, 0.46f, 0f));
        AddBox(shoulder, "Hombrera" + suffix, new Vector3(0f, 0.02f, 0f), new Vector3(0.19f, 0.15f, 0.19f), dark);
        AddBone(shoulder, "UpperArm" + suffix, UpperArmLength, 0.16f, suit);
        AddBox(shoulder, "Franja" + suffix, new Vector3(0f, -0.2f, 0f), new Vector3(0.17f, 0.05f, 0.17f), accent);

        var elbow = AddJoint(shoulder, "Elbow" + suffix, new Vector3(0f, -UpperArmLength, 0f));
        AddBox(elbow, "AnilloCodo" + suffix, Vector3.zero, new Vector3(0.16f, 0.08f, 0.16f), dark);
        AddBone(elbow, "Forearm" + suffix, ForearmLength, 0.145f, suit);
        AddBox(elbow, "Guante" + suffix, new Vector3(0f, -ForearmLength - 0.04f, 0.01f),
               new Vector3(0.17f, 0.15f, 0.18f), dark);
    }

    static void BuildLeg(Transform hips, string suffix, float side, Material suit, Material dark)
    {
        var hip = AddJoint(hips, "Leg" + suffix, new Vector3(side * 0.16f, -0.06f, 0f));
        AddBone(hip, "Thigh" + suffix, ThighLength, 0.23f, suit);

        var knee = AddJoint(hip, "Knee" + suffix, new Vector3(0f, -ThighLength, 0f));
        AddBox(knee, "AnilloRodilla" + suffix, Vector3.zero, new Vector3(0.23f, 0.09f, 0.23f), dark);
        AddBone(knee, "Shin" + suffix, ShinLength, 0.20f, suit);

        var ankle = AddJoint(knee, "Ankle" + suffix, new Vector3(0f, -ShinLength, 0f));
        AddBox(ankle, "Bota" + suffix, new Vector3(0f, -0.06f, 0.05f), new Vector3(0.23f, 0.14f, 0.31f), dark);
    }

    static Transform AddMesh(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    static Transform AddJoint(Transform parent, string name, Vector3 localPos)
    {
        var joint = new GameObject(name);
        joint.transform.SetParent(parent, false);
        joint.transform.localPosition = localPos;
        return joint.transform;
    }

    // El hueso cuelga hacia abajo desde su articulación, así rotar el pivote lo
    // hace girar desde la punta correcta y no desde su centro.
    static void AddBone(Transform joint, string name, float length, float thickness, Material mat)
    {
        AddBox(joint, name, new Vector3(0f, -length * 0.5f, 0f), new Vector3(thickness, length, thickness), mat);
    }

    static void AddBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        Object.DestroyImmediate(box.GetComponent<BoxCollider>());
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPos;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // El blanco del traje va a 0.80 y no a 1: con el bloom que tiene el post-procesado,
    // un blanco puro se quema y el personaje pierde toda la forma.
    // Coral encendido: el color que la paleta reserva para lo que quema.
    static Material FlareMaterial() => SuitMaterial("PS1_Flare", new Color(1f, 0.62f, 0.34f));

    static Material SuitMaterial(string assetName, Color color)
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

    static Material GetOrCreatePlayerMaterial()
    {
        Directory.CreateDirectory("Assets/_Project/Materials");
        AssetDatabase.Refresh();

        string path = "Assets/_Project/Materials/PS1_Player.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Cielo Profundo/PS1 Vertex Snap");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.102f, 0.055f, 0.239f)); // silueta oscura, "noche"
        mat.SetFloat("_SnapResolution", 140f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
