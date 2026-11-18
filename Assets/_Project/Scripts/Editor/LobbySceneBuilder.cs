using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// La nave: diorama con cámara fija en diagonal, al estilo isométrico. El cuarto
// entero entra en cuadro, así que la cámara no necesita seguir a nadie.
//
// Las dos paredes del lado de la cámara NO se construyen: es un corte, igual que
// una maqueta abierta. En su lugar van barreras invisibles para que el jugador no
// se salga del encuadre caminando.
public static class LobbySceneBuilder
{
    // Cuarto compacto: el interior es el protagonista y tiene que llenar la pantalla.
    // La nave exterior queda casi toda fuera de cuadro a propósito — se asoma en las
    // esquinas lo justo para saber que estás adentro de algo que vuela.
    const float HalfW = 8f;
    const float HalfD = 8f;
    const float WallHeight = 4.5f;
    const float WallThick = 0.4f;

    static Material hull, floorMat, accent, dark, screenMat, glass, markMat;
    static Material starlight, rust, cloth;

    [MenuItem("Cielo Profundo/Construir escena lobby nave")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        hull = ShipMaterial("PS1_Hull", new Color(0.60f, 0.63f, 0.69f));
        floorMat = ShipMaterial("PS1_HullFloor", new Color(0.34f, 0.37f, 0.43f));
        accent = ShipMaterial("PS1_HullAccent", new Color(0.94f, 0.45f, 0.28f));
        dark = ShipMaterial("PS1_HullDark", new Color(0.16f, 0.18f, 0.22f));
        screenMat = ShipMaterial("PS1_HullScreen", new Color(0.30f, 0.72f, 0.82f));
        glass = ShipMaterial("PS1_HullGlass", new Color(0.55f, 0.72f, 0.85f));
        markMat = ShipMaterial("PS1_HullMark", new Color(0.98f, 0.82f, 0.20f));

        // Las estelas usaban el material del vidrio, que es un azul sólido de casco: se
        // veían como barras pintadas cruzando la ventana en vez de como luz lejana.
        starlight = ShipMaterial("PS1_Starlight", new Color(0.90f, 0.95f, 1f));
        rust = ShipMaterial("PS1_HullRust", new Color(0.42f, 0.30f, 0.24f));
        cloth = ShipMaterial("PS1_HullCloth", new Color(0.31f, 0.36f, 0.34f));

        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(46f, -160f, 0f);
        light.intensity = 1.2f;

        // A la izquierda de la mesa de destino, despejado de mobiliario.
        var rig = PlayerRigFactory.Create(new Vector3(-1.4f, 1.5f, -0.6f));

        // Sin salto y con el escalón bajo: en el planeta el escalón va alto para
        // poder subir los cerros, pero acá eso te dejaba treparte a las cajas y a
        // las consolas y terminar caminando por arriba del mobiliario.
        var cc = rig.player.GetComponent<CharacterController>();
        cc.stepOffset = 0.25f;

        var ctrlSO = new SerializedObject(rig.controller);
        ctrlSO.FindProperty("canJump").boolValue = false;
        ctrlSO.ApplyModifiedPropertiesWithoutUndo();

        // Se reemplaza la cámara orbital por la fija. Se conserva el mismo GameObject
        // para que la referencia que PlayerController ya tiene siga siendo válida.
        var camGO = rig.camera.gameObject;
        Object.DestroyImmediate(rig.camera);
        var lobbyCam = camGO.AddComponent<LobbyCamera>();

        // Encuadrado sobre el cuarto, no sobre la nave. A esta distancia el interior
        // ocupa casi todo el cuadro y del casco solo asoman los bordes en las
        // esquinas, que es exactamente la composición de la referencia.
        var camSO = new SerializedObject(lobbyCam);
        camSO.FindProperty("position").vector3Value = new Vector3(17f, 17f, -17f);
        camSO.FindProperty("lookAt").vector3Value = new Vector3(0f, 1.2f, 0.8f);
        camSO.FindProperty("fieldOfView").floatValue = 36f;
        camSO.ApplyModifiedPropertiesWithoutUndo();

        EnvironmentDressingFactory.EnableAdditionalLights();
        EnvironmentDressingFactory.AddPostProcessing();
        EnvironmentDressingFactory.ApplySkybox(camGO.GetComponent<Camera>());

        var ship = new GameObject("Nave").transform;

        // Exterior primero: es la estructura sobre la que se apoya todo lo demás.
        var exterior = new GameObject("Casco").transform;
        exterior.SetParent(ship, false);
        BuildHullPlating(exterior);
        BuildNose(exterior);
        BuildWings(exterior);
        BuildTailAndEngines(exterior);
        BuildNavLights(exterior);

        BuildShell(ship);
        BuildFloorDetails(ship);
        BuildCeilingRig(ship);
        BuildOverhead(ship);
        BuildStencils(ship);

        // La bienvenida de la empresa: negro sobre blanco antes de que el jugador vea
        // la nave. Va como capa encima del lobby y no como escena aparte porque el
        // resultado en pantalla es el mismo y esto no toca ni el orden de escenas del
        // build ni la secuencia de salida del menu.
        var indGO = new GameObject("Induccion");
        indGO.transform.SetParent(ship, false);
        var induccion = indGO.AddComponent<InductionUI>();
        var indSO = new SerializedObject(induccion);
        indSO.FindProperty("playerController").objectReferenceValue = rig.controller;

        // Sin camara: en la nave la orbital se destruyo mas arriba y en su lugar hay
        // una fija que no lee raton, asi que no hay nada que apagar mientras dura la
        // bienvenida. Asignar rig.camera aca guardaria un componente ya destruido, que
        // en la escena se ve como null y parece un cable olvidado.
        indSO.ApplyModifiedPropertiesWithoutUndo();
        BuildProps(ship);
        BuildClutter(ship);

        BuildWindowWall(ship);
        BuildConsoleWall(ship);
        BuildStarStreaks(ship);

        var tableAnchor = BuildLevelSelectTable(ship);
        var shopAnchor = BuildShopKiosk(ship);
        var loreScreen = BuildLoreScreen(ship);

        // Sistemas: solo lo que la nave necesita. Nada de arañas, ruido, vida ni
        // bengalas — acá estás a salvo, así que ni se instancian.
        var systemsGO = new GameObject("GameSystems");
        var prompt = systemsGO.AddComponent<InteractionPromptUI>();

        // Zumbido de la nave. Arranca solo y no se apaga nunca: acá no hay transiciones,
        // es el fondo constante del único sitio donde estás a salvo.
        var hum = systemsGO.AddComponent<AmbientMusic>();
        var humSO = new SerializedObject(hum);
        humSO.FindProperty("clip").objectReferenceValue = PrepareShipAmbience();
        humSO.FindProperty("ambientVolume").floatValue = 0.32f;
        humSO.FindProperty("fadeInSeconds").floatValue = 3f;
        humSO.ApplyModifiedPropertiesWithoutUndo();

        // Aparece desde negro: es la otra mitad de la salida del menú. Sin esto el
        // fundido de salida no sirve de nada, porque la entrada seguiría siendo un golpe.
        var fade = systemsGO.AddComponent<ScreenFade>();
        var fadeSO = new SerializedObject(fade);
        fadeSO.FindProperty("fadeInOnStart").boolValue = true;
        fadeSO.FindProperty("startDuration").floatValue = 1.4f;
        fadeSO.ApplyModifiedPropertiesWithoutUndo();

        // Esfera armilar: se abre al confirmar destino y decide si bajas
        // identificado o a ciegas. Se crea antes que el selector porque lo referencia.
        var surveyUI = systemsGO.AddComponent<ArmillarySurveyUI>();
        SetPlayerController(surveyUI, rig.controller);

        var selectUI = systemsGO.AddComponent<LevelSelectUI>();
        SetPlayerController(selectUI, rig.controller);

        var selectSO = new SerializedObject(selectUI);
        selectSO.FindProperty("survey").objectReferenceValue = surveyUI;
        selectSO.ApplyModifiedPropertiesWithoutUndo();

        // Catálogo de la ruta: en la nave no hay Inventory, así que lee el archivo
        // que sobrevive entre escenas. Es donde se revisa lo juntado hasta ahora.
        var catalogUI = systemsGO.AddComponent<InventoryUI>();
        var catalogSO = new SerializedObject(catalogUI);
        catalogSO.FindProperty("playerController").objectReferenceValue = rig.controller;
        catalogSO.ApplyModifiedPropertiesWithoutUndo();

        var shopUI = systemsGO.AddComponent<ShopUI>();
        SetPlayerController(shopUI, rig.controller);

        var missionUI = systemsGO.AddComponent<MissionBriefUI>();
        SetPlayerController(missionUI, rig.controller);

        // Los tres puestos usan el mismo componente de proximidad: cambia el panel
        // que abren y la etiqueta que muestran.
        // El signo flota sobre la superficie que se usa, no sobre el ancla: el ancla
        // de la pantalla de misión está a media pared y el signo quedaría en el techo.
        var tableMark = AddInteractMarker(ship, "SignoMesa", new Vector3(1.2f, 1.85f, 0.2f), 0f);
        var shopMark = AddInteractMarker(ship, "SignoTienda", new Vector3(5.2f, 1.85f, HalfD - 1.7f), 1.1f);
        var loreMark = AddInteractMarker(ship, "SignoLore", new Vector3(-0.4f, 1.95f, HalfD - 1.6f), 2.2f);

        AddTerminal(tableAnchor, rig.player.transform, prompt, selectUI, "elegir destino", 3f, tableMark);
        AddTerminal(shopAnchor, rig.player.transform, prompt, shopUI, "abrir suministros", 3f, shopMark);
        AddTerminal(loreScreen, rig.player.transform, prompt, missionUI, "leer informe de misión", 4.2f, loreMark);

        // La misma pausa que en la superficie. La nave encerraba igual: una vez dentro
        // de la partida no había ninguna salida al menú principal.
        var pausaGO = new GameObject("Pausa");
        var pausa = pausaGO.AddComponent<PauseMenuUI>();
        var pausaSO = new SerializedObject(pausa);
        pausaSO.FindProperty("playerController").objectReferenceValue = rig.controller;
        // Igual que la bienvenida: la camara de la nave es fija y no lee raton.
        pausaSO.FindProperty("onSurface").boolValue = false;
        pausaSO.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory("Assets/_Project/Scenes");
        string scenePath = "Assets/_Project/Scenes/Ship_Lobby.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        // La nave va justo detrás del menú principal, o primera si el menú todavía no
        // existe. Antes quedaba de última, detrás del sandbox de pruebas, así que un
        // build arrancaba en la escena de pruebas del controlador.
        const string menuPath = "Assets/_Project/Scenes/Main_Menu.unity";
        var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        buildScenes.RemoveAll(s => s.path == scenePath);

        int menuIndex = buildScenes.FindIndex(s => s.path == menuPath);
        buildScenes.Insert(menuIndex + 1, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = buildScenes.ToArray();

        Debug.Log("Lobby construido en " + scenePath + " (pantalla de lore: " + loreScreen.name + ")");
    }

    // Este venía en IMA ADPCM de 4 bits a 11 kHz, así que hubo que decodificarlo para
    // poder medirlo. Traía 4,5 segundos de entrada desde silencio y una caída al final:
    // en bucle eso daba un bajón cada veinticinco segundos. Ya viene recortado.
    //
    // Pesa medio mega, así que va cargado en memoria y no por streaming: para un archivo
    // chico que suena todo el tiempo, el streaming cuesta más de lo que ahorra.
    static AudioClip PrepareShipAmbience()
    {
        const string ruta = "Assets/_Project/Audio/Musica/AmbienteNave.wav";

        var importer = AssetImporter.GetAtPath(ruta) as AudioImporter;
        if (importer == null)
        {
            Debug.LogWarning("No encontré el ambiente de la nave en " + ruta);
            return null;
        }

        var ajustes = importer.defaultSampleSettings;
        ajustes.loadType = AudioClipLoadType.CompressedInMemory;
        ajustes.compressionFormat = AudioCompressionFormat.Vorbis;
        ajustes.quality = 0.5f;
        ajustes.preloadAudioData = true;

        importer.defaultSampleSettings = ajustes;
        importer.forceToMono = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
    }

    static void SetPlayerController(HoloPanel panel, PlayerController controller)
    {
        var so = new SerializedObject(panel);
        so.FindProperty("playerController").objectReferenceValue = controller;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AddTerminal(Transform anchor, Transform player, InteractionPromptUI prompt,
                            HoloPanel panel, string label, float range = 3f, GameObject marker = null)
    {
        var terminal = anchor.gameObject.AddComponent<PanelTerminal>();
        var so = new SerializedObject(terminal);
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("prompt").objectReferenceValue = prompt;
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("actionLabel").stringValue = label;
        so.FindProperty("interactRange").floatValue = range;
        so.FindProperty("marker").objectReferenceValue = marker;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Signo de exclamación amarillo flotante, con el mismo BobSpin que llevan los
    // objetos recogibles del planeta: sube, baja y gira igual que ellos.
    //
    // El palo tiene sección cuadrada a propósito. Al girar sobre su eje, una placa
    // plana desaparecería de canto; con volumen se lee como "!" desde cualquier lado.
    static GameObject AddInteractMarker(Transform parent, string name, Vector3 pos, float phase)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;

        AddDecor(root.transform, "Palo", pos + new Vector3(0f, 0.22f, 0f),
                 new Vector3(0.15f, 0.44f, 0.15f), markMat);
        AddDecor(root.transform, "Punto", pos + new Vector3(0f, -0.12f, 0f),
                 new Vector3(0.15f, 0.15f, 0.15f), markMat);

        var glowGO = new GameObject("Brillo");
        glowGO.transform.SetParent(root.transform, false);
        glowGO.transform.position = pos;
        var glow = glowGO.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.85f, 0.35f);
        glow.range = 2.6f;
        glow.intensity = 1.4f;

        // Los mismos valores que Collectible traía adentro, con la fase corrida para
        // que los tres signos no floten sincronizados.
        root.AddComponent<BobSpin>().Configure(30f, 0.12f, 1.4f, phase);
        return root;
    }

    // Piso y las dos paredes del fondo. Las dos del lado de la cámara quedan como
    // barreras invisibles: bloquean pero no tapan la vista.
    static void BuildShell(Transform parent)
    {
        AddBlock(parent, "Piso", new Vector3(0f, -0.2f, 0f), new Vector3(HalfW * 2f, 0.4f, HalfD * 2f), floorMat);

        AddBlock(parent, "ParedFondoZ", new Vector3(0f, WallHeight * 0.5f, HalfD),
                 new Vector3(HalfW * 2f, WallHeight, WallThick), hull);

        AddInvisibleWall(parent, "BarreraFrenteZ", new Vector3(0f, WallHeight * 0.5f, -HalfD),
                         new Vector3(HalfW * 2f, WallHeight, WallThick));
        AddInvisibleWall(parent, "BarreraDerechaX", new Vector3(HalfW, WallHeight * 0.5f, 0f),
                         new Vector3(WallThick, WallHeight, HalfD * 2f));
    }

    // Conductos naranjas recorriendo el piso: la firma visual más reconocible de la
    // referencia y lo que más ata la sala visualmente.
    static void BuildFloorDetails(Transform parent)
    {
        AddDecor(parent, "ConductoA", new Vector3(0f, 0.03f, 4.5f), new Vector3(HalfW * 2f - 1.6f, 0.06f, 0.22f), accent);
        AddDecor(parent, "ConductoB", new Vector3(-5.5f, 0.03f, 0f), new Vector3(0.22f, 0.06f, HalfD * 2f - 2f), accent);
        AddDecor(parent, "ConductoC", new Vector3(4.2f, 0.03f, -2f), new Vector3(0.22f, 0.06f, 9f), accent);

        for (int i = 0; i < 5; i++)
        {
            float x = Mathf.Lerp(-HalfW + 1.5f, HalfW - 1.5f, i / 4f);
            AddDecor(parent, "JuntaPiso" + i, new Vector3(x, 0.02f, 0f),
                     new Vector3(0.08f, 0.04f, HalfD * 2f - 1f), dark);
        }
    }

    // Mobiliario suelto para que el piso no quede vacío. Todo bajo, para no tapar al
    // personaje desde este ángulo de cámara.
    static void BuildProps(Transform parent)
    {
        var props = new GameObject("Mobiliario").transform;
        props.SetParent(parent, false);

        // Cajas contra la esquina del frente izquierdo.
        AddBlock(props, "CajaA", new Vector3(-4.2f, 0.45f, -3.8f), new Vector3(1.2f, 0.9f, 1.2f), dark);
        AddBlock(props, "CajaB", new Vector3(-3f, 0.35f, -4.2f), new Vector3(1f, 0.7f, 1f), dark);
        AddDecor(props, "CintaCajaA", new Vector3(-4.2f, 0.78f, -3.8f), new Vector3(1.25f, 0.08f, 1.25f), accent);
        AddDecor(props, "CintaCajaB", new Vector3(-3f, 0.6f, -4.2f), new Vector3(1.05f, 0.07f, 1.05f), accent);

        // Banco largo mirando al ventanal.
        AddBlock(props, "Banco", new Vector3(-3.1f, 0.3f, 1.8f), new Vector3(0.85f, 0.58f, 2.8f), hull);
        AddDecor(props, "BancoFilo", new Vector3(-3.1f, 0.6f, 1.8f), new Vector3(0.9f, 0.06f, 2.85f), dark);

        // Mesa baja suelta hacia el frente, como la de la referencia.
        AddBlock(props, "MesaBaja", new Vector3(0.4f, 0.35f, -3.6f), new Vector3(2.2f, 0.7f, 1.2f), dark);
        AddDecor(props, "MesaBajaFilo", new Vector3(0.4f, 0.72f, -3.6f), new Vector3(2.25f, 0.06f, 1.25f), accent);

        // Marcas de seguridad en el piso alrededor de la mesa de destino.
        AddDecor(props, "MarcaMesa", new Vector3(1.2f, 0.025f, 0.2f), new Vector3(3.2f, 0.05f, 2.4f), dark);
    }

    // Sin nada de geometría arriba: las vigas y luminarias que había antes cruzaban
    // por delante del jugador desde este ángulo de cámara y lo tapaban. Quedan solo
    // las luces, que iluminan igual y no se ven.
    // Lo que hace que una sala parezca habitada y no montada.
    //
    // Una nave de verdad no está ordenada: hay cosas apoyadas donde se dejaron, del
    // tamaño de una mano, sin alinear con nada. Eso es exactamente lo que faltaba —
    // había mobiliario, pero ni un solo objeto que implicara a una persona.
    //
    // Todo va sin collider y bajo: son detalles, no obstáculos, y desde este ángulo de
    // cámara cualquier cosa alta taparía al personaje.
    static void BuildClutter(Transform parent)
    {
        var props = new GameObject("Trastos").transform;
        props.SetParent(parent, false);

        // Sobre la mesa baja: una taza, una bandeja y un par de tablillas tiradas. Nada
        // paralelo a nada, que es la diferencia entre dejado ahí y colocado ahí.
        AddDecor(props, "Bandeja", new Vector3(0.15f, 0.74f, -3.5f),
                 new Vector3(0.62f, 0.04f, 0.44f), dark, new Vector3(0f, 12f, 0f));
        AddDecor(props, "Taza", new Vector3(0.95f, 0.79f, -3.35f),
                 new Vector3(0.14f, 0.16f, 0.14f), hull);
        AddDecor(props, "AsaTaza", new Vector3(1.04f, 0.79f, -3.35f),
                 new Vector3(0.05f, 0.09f, 0.05f), hull);
        AddDecor(props, "TablillaA", new Vector3(-0.35f, 0.735f, -3.85f),
                 new Vector3(0.5f, 0.03f, 0.36f), screenMat, new Vector3(0f, -21f, 0f));
        AddDecor(props, "TablillaB", new Vector3(-0.28f, 0.765f, -3.78f),
                 new Vector3(0.46f, 0.03f, 0.33f), dark, new Vector3(0f, 7f, 0f));

        // Un casco de repuesto encima de la caja. Es el objeto que más dice que acá
        // trabaja alguien, porque es el mismo que llevas puesto abajo.
        var casco = new GameObject("CascoRepuesto");
        casco.transform.SetParent(props, false);
        casco.transform.position = new Vector3(-4.2f, 1.08f, -3.8f);
        casco.transform.rotation = Quaternion.Euler(0f, 34f, 0f);
        casco.AddComponent<MeshFilter>().sharedMesh =
            LowPolyMeshFactory.CreateFacetedSphere(10, 7, 0.23f);
        casco.AddComponent<MeshRenderer>().sharedMaterial = hull;
        AddDecor(props, "VisorRepuesto", new Vector3(-4.05f, 1.06f, -3.62f),
                 new Vector3(0.3f, 0.14f, 0.12f), markMat, new Vector3(0f, 34f, 0f));

        // Botas al pie del banco, y no de a pares perfectos.
        AddDecor(props, "BotaA", new Vector3(-2.5f, 0.09f, 1.0f),
                 new Vector3(0.2f, 0.18f, 0.36f), dark, new Vector3(0f, 15f, 0f));
        AddDecor(props, "BotaB", new Vector3(-2.32f, 0.09f, 1.22f),
                 new Vector3(0.2f, 0.18f, 0.36f), dark, new Vector3(0f, -38f, 0f));

        // Traje colgado en la pared del fondo, con su percha.
        AddDecor(props, "PerchaBarra", new Vector3(6.5f, 2.55f, HalfD - 0.5f),
                 new Vector3(1.1f, 0.06f, 0.06f), hull);
        AddDecor(props, "TrajeColgado", new Vector3(6.5f, 1.75f, HalfD - 0.55f),
                 new Vector3(0.62f, 1.5f, 0.22f), cloth);
        AddDecor(props, "TrajeHombros", new Vector3(6.5f, 2.4f, HalfD - 0.55f),
                 new Vector3(0.78f, 0.16f, 0.26f), cloth);
        AddDecor(props, "TrajeFranja", new Vector3(6.5f, 1.95f, HalfD - 0.66f),
                 new Vector3(0.64f, 0.07f, 0.04f), accent);

        // Caja de herramientas abierta y un rollo de cable en el suelo.
        AddBlock(props, "CajaHerramientas", new Vector3(5.6f, 0.16f, 1.9f),
                 new Vector3(0.7f, 0.32f, 0.42f), rust);
        AddDecor(props, "TapaHerramientas", new Vector3(5.6f, 0.5f, 2.14f),
                 new Vector3(0.7f, 0.34f, 0.04f), rust, new Vector3(24f, 0f, 0f));
        AddDecor(props, "RolloCable", new Vector3(6.3f, 0.12f, 1.2f),
                 new Vector3(0.62f, 0.16f, 0.62f), dark, new Vector3(0f, 18f, 0f));
        AddDecor(props, "RolloCableInt", new Vector3(6.3f, 0.14f, 1.2f),
                 new Vector3(0.3f, 0.18f, 0.3f), floorMat, new Vector3(0f, 18f, 0f));

        // Dos cajas más, giradas y de tamaños distintos. Apiladas a escuadra se leen
        // como decorado; torcidas, como carga que alguien arrimó con prisa.
        AddBlock(props, "CajaC", new Vector3(-5.4f, 0.32f, -1.4f),
                 new Vector3(0.9f, 0.64f, 0.9f), dark);
        AddDecor(props, "CintaCajaC", new Vector3(-5.4f, 0.56f, -1.4f),
                 new Vector3(0.95f, 0.07f, 0.95f), accent);
        AddBlock(props, "CajaD", new Vector3(-5.1f, 0.86f, -1.55f),
                 new Vector3(0.62f, 0.44f, 0.62f), rust);
        AddDecor(props, "CajaDGiro", new Vector3(-5.1f, 0.86f, -1.55f),
                 new Vector3(0.66f, 0.06f, 0.66f), accent, new Vector3(0f, 17f, 0f));
    }

    // Cañerías, cables y una rejilla por el techo y las paredes.
    //
    // Es lo que convierte un cajón en el interior de algo construido: por dentro de una
    // nave pasan cosas, y esas cosas se ven. Sin esto las paredes son planos lisos, que
    // es justo lo que se lee como sala de pruebas.
    static void BuildOverhead(Transform parent)
    {
        var rig = new GameObject("Instalaciones").transform;
        rig.SetParent(parent, false);

        // Los cables van por la PARED, no por el techo.
        //
        // Es que no hay techo: la sala se mira desde arriba en diorama, así que un cable
        // tendido a la altura del techo queda colgando en el aire sobre nada. Pegados a
        // la pared del fondo hacen el mismo trabajo —decir que por dentro de la nave
        // pasan cosas— y encima se ven, que a esa altura no pasaba.
        const float y = 3.55f;
        const float z = HalfD - WallThick * 0.5f - 0.12f;

        AddDecor(rig, "CableParedA", new Vector3(-1.2f, y, z),
                 new Vector3(HalfW * 2f - 3.4f, 0.09f, 0.09f), dark);
        AddDecor(rig, "CableParedB", new Vector3(-1.4f, y - 0.13f, z),
                 new Vector3(HalfW * 2f - 4.2f, 0.07f, 0.07f), rust);

        // Abrazaderas con separación irregular: repartidas por igual delatarían que las
        // puso un bucle.
        float[] grapas = { -6.4f, -3.9f, -0.7f, 2.1f, 4.8f };
        foreach (float x in grapas)
            AddDecor(rig, "Grapa", new Vector3(x, y, z - 0.05f),
                     new Vector3(0.14f, 0.3f, 0.08f), hull);

        // Conducto grueso bajando por el rincón hasta el suelo: ata el techo con la
        // pared, que si no se leen como dos superficies sin relación.
        AddDecor(rig, "ConductoVertical", new Vector3(7.1f, 2.3f, HalfD - 0.55f),
                 new Vector3(0.34f, 4.4f, 0.34f), hull);
        AddDecor(rig, "AnilloConductoA", new Vector3(7.1f, 1.2f, HalfD - 0.55f),
                 new Vector3(0.44f, 0.12f, 0.44f), dark);
        AddDecor(rig, "AnilloConductoB", new Vector3(7.1f, 3.1f, HalfD - 0.55f),
                 new Vector3(0.44f, 0.12f, 0.44f), dark);

        // Rejilla de ventilación en la pared del fondo, con sus lamas.
        AddDecor(rig, "MarcoRejilla", new Vector3(-6.4f, 3.3f, HalfD - 0.42f),
                 new Vector3(1.5f, 0.9f, 0.08f), dark);
        for (int i = 0; i < 5; i++)
            AddDecor(rig, "Lama" + i, new Vector3(-6.4f, 3.3f - 0.3f + i * 0.15f, HalfD - 0.47f),
                     new Vector3(1.34f, 0.06f, 0.04f), hull);

        // Franjas de peligro junto al ventanal: señalización de la que se pinta donde hay
        // algo que puede matarte. Y ahí afuera hay vacío.
        for (int i = 0; i < 7; i++)
            AddDecor(rig, "Chevron" + i,
                     new Vector3(-HalfW + 0.55f, 0.32f, -2.7f + i * 0.9f),
                     new Vector3(0.05f, 0.34f, 0.42f), i % 2 == 0 ? markMat : dark,
                     new Vector3(0f, 0f, 34f));
    }

    // Rotulación pintada en el casco.
    //
    // Esto es lo que más ataca la sensación de sala genérica, y no por bonito: una nave
    // sin nombre es un decorado, una con matrícula es un sitio concreto donde pasa algo.
    // Además ata el lobby al lore — la sexta cuadrilla eres tú, y lo dice la pared antes
    // de que lo diga ninguna ficha.
    static void BuildStencils(Transform parent)
    {
        var root = new GameObject("Rotulacion").transform;
        root.SetParent(parent, false);

        // Sobre el riel de la pared de consolas, que es la única franja despejada.
        AddStencil(root, "PARALAX  ·  CARTOGRAFÍA ESTELAR",
                   new Vector3(-1.6f, 4.16f, HalfD - WallThick * 0.5f - 0.06f),
                   new Vector3(0f, 180f, 0f), 46, new Color(0.74f, 0.79f, 0.86f), 620f);

        // Matrícula junto al ventanal, del tamaño en que se pintan de verdad: grandes,
        // para leerlas desde fuera.
        AddStencil(root, "PRL-6",
                   new Vector3(-HalfW + 0.28f, 3.15f, 3.4f),
                   new Vector3(0f, 90f, 0f), 150, new Color(0.62f, 0.66f, 0.72f), 420f);

        AddStencil(root, "CUADRILLA VI",
                   new Vector3(-HalfW + 0.28f, 2.55f, 3.4f),
                   new Vector3(0f, 90f, 0f), 52, accent.GetColor("_BaseColor"), 420f);

        // Aviso de servicio junto a la rejilla, en chico. Los carteles aburridos son los
        // que más convencen: nadie decora una nave con advertencias de mantenimiento.
        AddStencil(root, "PURGA DE AIRE  ·  NO OBSTRUIR",
                   new Vector3(-6.4f, 2.72f, HalfD - WallThick * 0.5f - 0.06f),
                   new Vector3(0f, 180f, 0f), 34, new Color(0.55f, 0.58f, 0.64f), 400f);
    }

    // Un rótulo pintado: un objeto normal que lo coloca, y colgando de él el lienzo.
    //
    // Los dos niveles son la solución a un fallo que me costó dos intentos. Un
    // RectTransform NO guarda su X y su Y como posición: las deriva de anchoredPosition,
    // que nace en cero. Da igual si pones la posición antes de añadir el Canvas o después
    // —probé las dos— porque al serializar la escena manda el anclaje y las coordenadas
    // se pierden. Los cuatro rótulos terminaban en el centro de la sala, a ras de suelo.
    //
    // Con un Transform corriente encima, la colocación vive donde sí se conserva, y el
    // lienzo se queda quieto en el cero local de su padre.
    static void AddStencil(Transform parent, string texto, Vector3 pos, Vector3 euler,
                           int cuerpo, Color color, float ancho)
    {
        var anchor = new GameObject("Rotulo").transform;
        anchor.SetParent(parent, false);
        anchor.position = pos;
        anchor.rotation = Quaternion.Euler(euler);

        // La escala va acá, en el objeto de colocación: es la que convierte unidades de
        // interfaz en metros. Con 0.01, un cuerpo de 46 mide unos 46 centímetros de alto,
        // que es tamaño de rotulación de casco.
        anchor.localScale = Vector3.one * 0.01f;

        var canvasGO = new GameObject("Lienzo", typeof(RectTransform), typeof(Canvas));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt = (RectTransform)canvasGO.transform;
        rt.SetParent(anchor, false);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(ancho, cuerpo * 1.5f);

        var text = HoloUI.CreateText(rt, "Texto", cuerpo, TextAnchor.MiddleCenter, color);
        text.text = texto;
        HoloUI.Stretch(text.rectTransform);
    }

    // El desgaste del suelo se quitó, y prefiero explicar por qué a dejarlo a medias.
    //
    // Eran cajas oscuras giradas sobre el piso. La idea —marcar por dónde se camina— es
    // buena, pero un rectángulo de bordes duros no se lee como suciedad: se lee como una
    // chapa distinta atornillada ahí. En la captura parecían paneles del suelo, y un
    // detalle que se lee como otra cosa es peor que no tenerlo.
    //
    // Hacerlo bien pide bordes difusos, y eso es una calcomanía con textura y material
    // transparente: no lo tiene el proyecto y no es un cambio de cinco minutos. El suelo
    // ya lleva los conductos naranjas y las juntas, que es bastante carga visual.

    static void BuildCeilingRig(Transform parent)
    {
        var rig = new GameObject("Iluminacion").transform;
        rig.SetParent(parent, false);

        // Ni repartidas por igual ni todas del mismo color, y esto es lo que más pesa
        // contra el aspecto de sala generada.
        //
        // Cuatro luces idénticas a intervalos exactos en la línea central es una regla
        // que el ojo caza en un segundo: se lee como una cuadrícula, no como un sitio.
        // Las de verdad se ponen donde hacen falta —sobre el puesto de trabajo, sobre la
        // mesa— y una siempre está más gastada que las otras.
        //
        // {x, z, alcance, intensidad, calidez}
        float[][] puestos =
        {
            new[] { -0.6f,  6.2f, 11f, 2.6f, 0f },   // sobre las consolas
            new[] {  1.4f,  0.4f, 10f, 2.2f, 0f },   // sobre la mesa de destino
            new[] {  4.6f, -3.2f,  9f, 1.5f, 1f },   // rincón de la tienda, más cálido
            new[] { -4.4f, -3.6f,  8f, 1.1f, 0f },   // rincón de las cajas, floja
        };

        for (int i = 0; i < puestos.Length; i++)
        {
            var p = puestos[i];

            var lightGO = new GameObject("LuzTecho" + i);
            lightGO.transform.SetParent(rig, false);
            lightGO.transform.position = new Vector3(p[0], WallHeight - 0.65f, p[1]);

            var pointLight = lightGO.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = Color.Lerp(new Color(0.72f, 0.86f, 0.95f),
                                          new Color(0.99f, 0.78f, 0.52f), p[4]);
            pointLight.range = p[2];
            pointLight.intensity = p[3];

            // Cada luz con su lámpara visible. Una habitación iluminada por focos que no
            // están en ninguna parte se siente irreal aunque no sepas decir por qué.
            AddDecor(rig, "Luminaria" + i, new Vector3(p[0], WallHeight - 0.28f, p[1]),
                     new Vector3(1.5f, 0.12f, 0.34f), dark);
            AddDecor(rig, "Tubo" + i, new Vector3(p[0], WallHeight - 0.38f, p[1]),
                     new Vector3(1.25f, 0.06f, 0.16f), glass);
        }
    }

    // Pared del fondo en X con el ventanal: se arma como un hueco real (cuatro
    // segmentos alrededor del vano), no como una pared con una textura de ventana.
    static void BuildWindowWall(Transform parent)
    {
        const float x = -HalfW;
        const float opening = 6f;

        // Cada segmento se lleva la MITAD de lo que sobra, no todo: con el largo
        // completo los dos juntos cubrían la pared entera y el vano desaparecía.
        float sideLen = (HalfD * 2f - opening) * 0.5f;
        float sideCenter = opening * 0.5f + sideLen * 0.5f;

        AddBlock(parent, "VentanaLateralA", new Vector3(x, WallHeight * 0.5f, -sideCenter),
                 new Vector3(WallThick, WallHeight, sideLen), hull);
        AddBlock(parent, "VentanaLateralB", new Vector3(x, WallHeight * 0.5f, sideCenter),
                 new Vector3(WallThick, WallHeight, sideLen), hull);
        AddBlock(parent, "VentanaDintel", new Vector3(x, 4.3f, 0f),
                 new Vector3(WallThick, 1.4f, opening), hull);
        AddBlock(parent, "VentanaZocalo", new Vector3(x, 0.5f, 0f),
                 new Vector3(WallThick, 1f, opening), hull);

        AddDecor(parent, "MarcoVentanaSup", new Vector3(x + 0.25f, 3.55f, 0f), new Vector3(0.1f, 0.14f, opening), accent);
        AddDecor(parent, "MarcoVentanaInf", new Vector3(x + 0.25f, 1.02f, 0f), new Vector3(0.1f, 0.14f, opening), accent);

        for (int i = 0; i < 3; i++)
        {
            float z = Mathf.Lerp(-2f, 2f, i / 2f);
            AddDecor(parent, "MontanteVentana" + i, new Vector3(x + 0.2f, 2.3f, z),
                     new Vector3(0.12f, 2.5f, 0.12f), dark);
        }

        // Baranda delante del ventanal, como en la referencia.
        AddDecor(parent, "BarandaSup", new Vector3(x + 1.3f, 1.1f, 0f), new Vector3(0.08f, 0.08f, 6f), hull);
        for (int i = 0; i < 4; i++)
        {
            float z = Mathf.Lerp(-2.8f, 2.8f, i / 3f);
            AddDecor(parent, "BarandaPoste" + i, new Vector3(x + 1.3f, 0.55f, z),
                     new Vector3(0.07f, 1.1f, 0.07f), hull);
        }
    }

    // Banco de consolas sobre la pared del fondo en Z.
    static void BuildConsoleWall(Transform parent)
    {
        const float z = HalfD - 0.35f;

        // Banco corrido en vez de consolas sueltas: la referencia tiene una tirada
        // continua de paneles, no muebles separados con huecos entre medio.
        AddConsole(parent, "Consola0", new Vector3(-4.3f, 0f, z));
        AddConsole(parent, "Consola1", new Vector3(3.9f, 0f, z));

        // Paneles a media altura llenando el tramo entre la pantalla grande y el borde.
        for (int i = 0; i < 4; i++)
        {
            float x = Mathf.Lerp(2.4f, 5.4f, i / 3f);
            AddDecor(parent, "PanelMuro" + i, new Vector3(x, 2.9f, z + 0.12f),
                     new Vector3(0.85f, 0.7f, 0.06f), i == 2 ? accent : screenMat);
        }

        AddDecor(parent, "RielSuperior", new Vector3(0f, 3.9f, z + 0.1f),
                 new Vector3(HalfW * 2f - 1f, 0.12f, 0.12f), accent);
        AddDecor(parent, "ZocaloMuro", new Vector3(0f, 0.12f, z + 0.05f),
                 new Vector3(HalfW * 2f - 0.6f, 0.24f, 0.1f), dark);
    }

    // Una consola: base inclinada con pantalla, columna trasera con paneles chicos.
    static void AddConsole(Transform parent, string name, Vector3 basePosition)
    {
        var anchor = new GameObject(name).transform;
        anchor.SetParent(parent, false);
        anchor.position = basePosition;

        AddBlock(anchor, "Cuerpo", basePosition + new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 1f, 0.9f), hull);
        AddDecor(anchor, "Tablero", basePosition + new Vector3(0f, 1.12f, -0.15f),
                 new Vector3(2.2f, 0.06f, 0.7f), dark, new Vector3(-22f, 0f, 0f));
        AddDecor(anchor, "Pantalla", basePosition + new Vector3(0f, 1.2f, -0.2f),
                 new Vector3(1.6f, 0.04f, 0.44f), screenMat, new Vector3(-22f, 0f, 0f));

        AddBlock(anchor, "Torre", basePosition + new Vector3(0f, 1.9f, 0.28f), new Vector3(2.4f, 2.4f, 0.35f), hull);

        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 3; c++)
            {
                Vector3 p = basePosition + new Vector3(Mathf.Lerp(-0.72f, 0.72f, c / 2f), 1.45f + r * 0.8f, 0.08f);
                AddDecor(anchor, $"Panel{r}{c}", p, new Vector3(0.6f, 0.5f, 0.05f),
                         (r + c) % 3 == 0 ? accent : screenMat);
            }
    }

    static void BuildStarStreaks(Transform parent)
    {
        // Nacen fuera del casco y avanzan hasta el plano del ventanal. Como la pared
        // tapa todo lo demás, el efecto solo se ve por el hueco.
        var go = new GameObject("Estelas");
        go.transform.SetParent(parent, false);
        // Todo el recorrido queda FUERA de la sala, y esto había que corregirlo.
        //
        // Antes nacían en x = -10,8 y viajaban setenta unidades hacia +X, o sea que
        // cruzaban la sala entera de lado a lado. Mirando por el ventanal desde dentro no
        // se notaba, porque la pared tapa el resto del recorrido; pero la cámara del
        // lobby mira desde arriba y por encima de las paredes, así que se veían las
        // estelas atravesando el suelo.
        //
        // Puestas en x = -30 y con recorrido de 42, van de -51 a -9: mueren una unidad
        // antes del ventanal. Se siguen viendo por el hueco y ya no entran.
        go.transform.position = new Vector3(-30f, 2.8f, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        var streaks = go.AddComponent<StarStreakField>();
        var so = new SerializedObject(streaks);
        so.FindProperty("streakMaterial").objectReferenceValue = starlight;
        so.FindProperty("spread").vector2Value = new Vector2(6f, 3.5f);
        so.FindProperty("distance").floatValue = 42f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Transform BuildLevelSelectTable(Transform parent)
    {
        var anchor = new GameObject("MesaDestino").transform;
        anchor.SetParent(parent, false);
        anchor.position = new Vector3(1.2f, 1f, 0.2f);

        Vector3 b = new Vector3(1.2f, 0f, 0.2f);
        AddBlock(anchor, "Pedestal", b + new Vector3(0f, 0.35f, 0f), new Vector3(1.1f, 0.7f, 1.1f), hull);
        AddDecor(anchor, "AnilloBase", b + new Vector3(0f, 0.72f, 0f), new Vector3(1.5f, 0.08f, 1.5f), accent);
        AddBlock(anchor, "Tablero", b + new Vector3(0f, 0.86f, 0f), new Vector3(2.2f, 0.12f, 1.5f), hull);
        AddDecor(anchor, "Proyeccion", b + new Vector3(0f, 0.95f, 0f), new Vector3(1.7f, 0.03f, 1.1f), screenMat);

        var glowGO = new GameObject("LuzMesa");
        glowGO.transform.SetParent(anchor, false);
        glowGO.transform.position = b + new Vector3(0f, 1.5f, 0f);
        var glow = glowGO.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.42f, 0.84f, 0.92f);
        glow.range = 8f;
        glow.intensity = 3f;

        return anchor;
    }

    static Transform BuildShopKiosk(Transform parent)
    {
        var anchor = new GameObject("Kiosco").transform;
        anchor.SetParent(parent, false);
        // Contra la pared del fondo, a la derecha de la pantalla de misión.
        Vector3 b = new Vector3(5.2f, 0f, HalfD - 1.1f);
        anchor.position = b;

        AddBlock(anchor, "Mostrador", b + new Vector3(0f, 0.55f, 0f), new Vector3(2.6f, 1.1f, 1.2f), hull);
        AddDecor(anchor, "BordeMostrador", b + new Vector3(0f, 1.13f, 0f), new Vector3(2.7f, 0.07f, 1.3f), accent);
        AddBlock(anchor, "Vitrina", b + new Vector3(0f, 1.9f, 0.45f), new Vector3(2.6f, 1.5f, 0.3f), dark);

        for (int i = 0; i < 3; i++)
        {
            AddDecor(anchor, "Estante" + i, b + new Vector3(Mathf.Lerp(-0.8f, 0.8f, i / 2f), 1.9f, 0.28f),
                     new Vector3(0.6f, 1.1f, 0.06f), screenMat);
        }

        var signGO = new GameObject("LetreroTienda");
        signGO.transform.SetParent(anchor, false);
        signGO.transform.position = b + new Vector3(0f, 3.1f, 0.3f);
        signGO.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        signGO.transform.localScale = Vector3.one * 0.006f;

        var canvas = signGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        signGO.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 130f);

        var panel = HoloUI.CreatePanel(signGO.transform, "Panel", new Vector2(400f, 120f), Vector2.zero,
                                       new Vector2(0.5f, 0.5f), HoloUI.FillDeep);
        var label = HoloUI.CreateText(panel, "Texto", 26, TextAnchor.MiddleCenter, HoloUI.Accent);
        HoloUI.Stretch(label.rectTransform, 10f);
        label.text = "SUMINISTROS";

        return anchor;
    }

    // Pantalla grande de pared: muestra el estado real de la ruta leyendo
    // RouteDatabase, así que no es un cartel decorativo.
    static Transform BuildLoreScreen(Transform parent)
    {
        // Grande y centrada en la pared del fondo: en la referencia es la pieza que
        // domina el muro, no un cartel más entre los paneles.
        AddDecor(parent, "MarcoPantalla", new Vector3(-0.4f, 2.85f, HalfD - 0.42f),
                 new Vector3(5.2f, 2.9f, 0.12f), dark);

        var mount = new GameObject("PantallaLore").transform;
        mount.SetParent(parent, false);
        mount.position = new Vector3(-0.4f, 2.85f, HalfD - 0.5f);
        mount.rotation = Quaternion.Euler(0f, 180f, 0f);
        mount.localScale = Vector3.one * 0.0088f;

        // No se agrega RectTransform a mano: el Canvas que crea RouteStatusScreen en
        // su Awake lo genera solo, y forzarlo acá reemplazaría el Transform actual.
        mount.gameObject.AddComponent<RouteStatusScreen>();
        return mount;
    }

    // ── Exterior ────────────────────────────────────────────────────────────────
    // Todo el casco va sin collider: el jugador queda encerrado por las paredes del
    // cuarto y las barreras invisibles, así que la geometría de afuera es puramente
    // visual y no tiene por qué participar de la física.

    // Casco: una base escalonada debajo del cuarto más un borde perimetral que sube
    // por fuera de las paredes. El escalonado es lo que le da volumen de nave en vez
    // de parecer una caja apoyada sobre una plancha.
    static void BuildHullPlating(Transform parent)
    {
        AddDecor(parent, "CascoBase", new Vector3(0f, -1.3f, 0f),
                 new Vector3(HalfW * 2f + 5f, 1.4f, HalfD * 2f + 5f), hull);
        AddDecor(parent, "CascoQuilla", new Vector3(0f, -2.3f, 0f),
                 new Vector3(HalfW * 2f - 1f, 1f, HalfD * 2f - 1f), dark);

        // Borde perimetral: cuatro bloques rodeando el cuarto, un poco más altos que
        // el piso para que se vea el filo del casco desde arriba.
        float rimY = 0.35f;
        float outer = 2.2f;
        AddDecor(parent, "BordeIzq", new Vector3(-HalfW - outer * 0.5f, rimY, 0f),
                 new Vector3(outer, 1.5f, HalfD * 2f + 5f), hull);
        AddDecor(parent, "BordeDer", new Vector3(HalfW + outer * 0.5f, rimY, 0f),
                 new Vector3(outer, 1.5f, HalfD * 2f + 5f), hull);
        AddDecor(parent, "BordeFondo", new Vector3(0f, rimY, HalfD + outer * 0.5f),
                 new Vector3(HalfW * 2f + 5f, 1.5f, outer), hull);
        AddDecor(parent, "BordeFrente", new Vector3(0f, rimY, -HalfD - outer * 0.5f),
                 new Vector3(HalfW * 2f + 5f, 1.5f, outer), hull);

        // Franjas naranjas en el filo del borde: la marca visual de la referencia.
        float stripeY = 1.06f;
        AddDecor(parent, "FranjaIzq", new Vector3(-HalfW - outer + 0.1f, stripeY, 0f),
                 new Vector3(0.18f, 0.16f, HalfD * 2f + 4f), accent);
        AddDecor(parent, "FranjaDer", new Vector3(HalfW + outer - 0.1f, stripeY, 0f),
                 new Vector3(0.18f, 0.16f, HalfD * 2f + 4f), accent);
        AddDecor(parent, "FranjaFondo", new Vector3(0f, stripeY, HalfD + outer - 0.1f),
                 new Vector3(HalfW * 2f + 4f, 0.16f, 0.18f), accent);
        AddDecor(parent, "FranjaFrente", new Vector3(0f, stripeY, -HalfD - outer + 0.1f),
                 new Vector3(HalfW * 2f + 4f, 0.16f, 0.18f), accent);

        // Placas de blindaje repartidas, para romper la superficie lisa.
        for (int i = 0; i < 6; i++)
        {
            float z = Mathf.Lerp(-HalfD + 1f, HalfD - 1f, i / 5f);
            AddDecor(parent, "PlacaIzq" + i, new Vector3(-HalfW - outer * 0.5f, 1.16f, z),
                     new Vector3(outer - 0.5f, 0.14f, 1.6f), dark);
            AddDecor(parent, "PlacaDer" + i, new Vector3(HalfW + outer * 0.5f, 1.16f, z),
                     new Vector3(outer - 0.5f, 0.14f, 1.6f), dark);
        }
    }

    // Morro con cabina, al frente (-Z). Se estrecha en tres escalones porque las
    // primitivas no tienen forma cónica.
    static void BuildNose(Transform parent)
    {
        float z0 = -HalfD - 2.2f;

        AddDecor(parent, "MorroA", new Vector3(0f, -0.5f, z0 - 1.8f), new Vector3(11f, 2.4f, 4f), hull);
        AddDecor(parent, "MorroB", new Vector3(0f, -0.7f, z0 - 4.6f), new Vector3(7.5f, 2f, 2.6f), hull);
        AddDecor(parent, "MorroC", new Vector3(0f, -0.9f, z0 - 6.5f), new Vector3(4.5f, 1.6f, 1.8f), hull);

        // Cabina: vidrio inclinado sobre el morro, como la de la referencia.
        AddDecor(parent, "CabinaBase", new Vector3(0f, 0.55f, z0 - 3.4f), new Vector3(5.6f, 1.5f, 3.4f), hull);
        AddDecor(parent, "CabinaVidrio", new Vector3(0f, 1.05f, z0 - 4.8f),
                 new Vector3(4.6f, 1.8f, 0.25f), glass, new Vector3(-34f, 0f, 0f));
        AddDecor(parent, "CabinaMarco", new Vector3(0f, 1.75f, z0 - 3.8f), new Vector3(5f, 0.2f, 0.8f), dark);

        AddDecor(parent, "FranjaMorroIzq", new Vector3(-2.7f, 0.4f, z0 - 3f),
                 new Vector3(0.16f, 0.16f, 4.5f), accent);
        AddDecor(parent, "FranjaMorroDer", new Vector3(2.7f, 0.4f, z0 - 3f),
                 new Vector3(0.16f, 0.16f, 4.5f), accent);
    }

    // Alas en flecha: cada una es una placa rotada en Y, más un borde de acento y
    // una góndola en la punta.
    static void BuildWings(Transform parent)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            string s = side < 0 ? "Izq" : "Der";

            AddDecor(parent, "Ala" + s, new Vector3(side * 10.5f, -0.6f, 1.2f),
                     new Vector3(9.5f, 1f, 6f), hull, new Vector3(0f, side * -18f, 0f));
            AddDecor(parent, "AlaFilo" + s, new Vector3(side * 10.5f, -0.08f, 1.2f),
                     new Vector3(9f, 0.14f, 1f), accent, new Vector3(0f, side * -18f, 0f));

            AddDecor(parent, "Gondola" + s, new Vector3(side * 14f, -0.4f, 2.6f),
                     new Vector3(2f, 1.6f, 4.6f), dark, new Vector3(0f, side * -18f, 0f));
            AddDecor(parent, "GondolaTapa" + s, new Vector3(side * 14f, 0.42f, 2.6f),
                     new Vector3(1.7f, 0.18f, 3.8f), hull, new Vector3(0f, side * -18f, 0f));

            // Alerón vertical en la punta.
            AddDecor(parent, "Aleron" + s, new Vector3(side * 14.3f, 1.1f, 4.2f),
                     new Vector3(0.28f, 2.4f, 2.6f), hull, new Vector3(0f, side * -18f, side * -12f));
        }
    }

    static void BuildTailAndEngines(Transform parent)
    {
        float z = HalfD + 2.2f;

        AddDecor(parent, "ColaBase", new Vector3(0f, -0.4f, z + 2.2f), new Vector3(12f, 2.2f, 4.5f), hull);
        AddDecor(parent, "ColaEstrecho", new Vector3(0f, -0.5f, z + 5f), new Vector3(8f, 1.8f, 3f), hull);

        // Deriva vertical.
        AddDecor(parent, "Deriva", new Vector3(0f, 2.6f, z + 4f), new Vector3(0.45f, 5.2f, 4f), hull,
                 new Vector3(-16f, 0f, 0f));
        AddDecor(parent, "DerivaFilo", new Vector3(0f, 4.9f, z + 3.3f), new Vector3(0.6f, 0.8f, 1.7f), accent,
                 new Vector3(-16f, 0f, 0f));

        for (int side = -1; side <= 1; side += 2)
        {
            string s = side < 0 ? "Izq" : "Der";
            AddDecor(parent, "Motor" + s, new Vector3(side * 3.8f, -0.3f, z + 6.2f),
                     new Vector3(2.8f, 2.8f, 4f), dark);
            AddDecor(parent, "MotorAro" + s, new Vector3(side * 3.8f, -0.3f, z + 8f),
                     new Vector3(3.2f, 3.2f, 0.45f), hull);

            var nozzle = AddDecor(parent, "Tobera" + s, new Vector3(side * 3.8f, -0.3f, z + 8.3f),
                                  new Vector3(2.2f, 2.2f, 0.35f), accent);

            var lightGO = new GameObject("LuzMotor" + s);
            lightGO.transform.SetParent(nozzle.transform, false);
            var engineGlow = lightGO.AddComponent<Light>();
            engineGlow.type = LightType.Point;
            engineGlow.color = new Color(1f, 0.55f, 0.28f);
            engineGlow.range = 16f;
            engineGlow.intensity = 3.2f;
        }
    }

    // Luces rojas de navegación: puntas de ala, morro y cola, cada una con su propio
    // desfase para que no parpadeen todas a la vez.
    static void BuildNavLights(Transform parent)
    {
        var red = ShipMaterial("PS1_NavLight", new Color(1f, 0.22f, 0.18f));

        Vector3[] spots =
        {
            new Vector3(-15f, 0.75f, 4.2f),
            new Vector3(15f, 0.75f, 4.2f),
            new Vector3(0f, 0.4f, -HalfD - 9.4f),
            new Vector3(0f, 5.4f, HalfD + 5f),
            new Vector3(-HalfW - 2.2f, 1.3f, -3f),
            new Vector3(HalfW + 2.2f, 1.3f, -3f),
        };

        for (int i = 0; i < spots.Length; i++)
        {
            var bulb = AddDecor(parent, "LuzNav" + i, spots[i], new Vector3(0.42f, 0.42f, 0.42f), red);

            var lightGO = new GameObject("Halo");
            lightGO.transform.SetParent(bulb.transform, false);
            var glow = lightGO.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.25f, 0.2f);
            glow.range = 9f;
            glow.intensity = 2.4f;

            var blink = bulb.AddComponent<NavLightBlink>();
            var blinkSO = new SerializedObject(blink);
            blinkSO.FindProperty("target").objectReferenceValue = bulb.GetComponent<Renderer>();
            blinkSO.FindProperty("glow").objectReferenceValue = glow;
            blinkSO.FindProperty("phase").floatValue = i * 0.27f;
            blinkSO.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static GameObject AddBlock(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = pos;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = mat;
        return box;
    }

    // Sin collider: detalles finos que no deben estorbar al caminar.
    static GameObject AddDecor(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = AddBlock(parent, name, pos, scale, mat);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject AddDecor(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, Vector3 euler)
    {
        var go = AddDecor(parent, name, pos, scale, mat);
        go.transform.rotation = Quaternion.Euler(euler);
        return go;
    }

    // Bloquea sin taparte la vista: es la pared que la cámara no debe ver.
    static void AddInvisibleWall(Transform parent, string name, Vector3 pos, Vector3 scale)
    {
        var go = AddBlock(parent, name, pos, scale, hull);
        Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(go.GetComponent<MeshFilter>());
    }

    static Material ShipMaterial(string assetName, Color color)
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
