using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProceduralPlanetSceneBuilder
{
    [MenuItem("Cielo Profundo/Construir escena planeta procedural")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        light.intensity = 1.0f;

        var rig = PlayerRigFactory.Create(new Vector3(0f, 3f, 0f));
        PlayerRigFactory.CreateGameSystems(rig);

        EnvironmentDressingFactory.EnableAdditionalLights();
        var globalVolume = EnvironmentDressingFactory.AddPostProcessing();
        var skyMaterial = EnvironmentDressingFactory.ApplySkybox(rig.camera.GetComponent<Camera>());

        Directory.CreateDirectory("Assets/_Project/Data/PlanetThemes");
        AssetDatabase.Refresh();

        string themePath = "Assets/_Project/Data/PlanetThemes/Planeta_Demo.asset";
        var theme = AssetDatabase.LoadAssetAtPath<PlanetTheme>(themePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<PlanetTheme>();
            theme.seed = 7;
            AssetDatabase.CreateAsset(theme, themePath);
        }

        var terrainGO = new GameObject("Terrain_Procedural");
        var generator = terrainGO.AddComponent<NoiseTerrainGenerator>();
        var generatorSO = new SerializedObject(generator);
        generatorSO.FindProperty("theme").objectReferenceValue = theme;
        generatorSO.FindProperty("playerToPlace").objectReferenceValue = rig.player.transform;
        generatorSO.ApplyModifiedPropertiesWithoutUndo();

        // Sesión: qué cuerpo de la ruta de Paralax estamos visitando. Fija la semilla
        // en su Awake, antes de que el generador arme el terreno en su Start.
        var sessionGO = new GameObject("PlanetSession");
        var session = sessionGO.AddComponent<PlanetSession>();
        var sessionSO = new SerializedObject(session);
        sessionSO.FindProperty("generator").objectReferenceValue = generator;
        sessionSO.ApplyModifiedPropertiesWithoutUndo();

        // HUD: radar de proximidad (siempre visible) y contador de fichas.
        // Aparece desde negro, igual que la nave. Sin esto el planeta entraba de golpe
        // y el descenso quedaba como el único corte duro que sobrevivía.
        var planetFade = new GameObject("Fundido").AddComponent<ScreenFade>();
        var planetFadeSO = new SerializedObject(planetFade);
        planetFadeSO.FindProperty("fadeInOnStart").boolValue = true;
        planetFadeSO.FindProperty("startDuration").floatValue = 1.6f;
        planetFadeSO.ApplyModifiedPropertiesWithoutUndo();

        var hudGO = new GameObject("HUD");
        var radar = hudGO.AddComponent<RadarUI>();
        var radarSO = new SerializedObject(radar);
        radarSO.FindProperty("player").objectReferenceValue = rig.player.transform;
        radarSO.FindProperty("facingReference").objectReferenceValue = rig.camera.transform;
        radarSO.ApplyModifiedPropertiesWithoutUndo();
        hudGO.AddComponent<CollectibleCounterUI>();
        hudGO.AddComponent<TransmissionUI>();

        // Aviso de descenso penalizado. Se apaga solo si la dificultad es 1, así que
        // en un descenso limpio no cuesta nada tenerlo puesto.
        hudGO.AddComponent<DescentWarningUI>();

        // Medidor de ruido: el sigilo era invisible hasta ahora.
        hudGO.AddComponent<NoiseMeterUI>();

        // Lámpara de casco con F. Solo en el planeta: la nave está iluminada.
        var lamp = rig.player.AddComponent<Flashlight>();
        var lampSO = new SerializedObject(lamp);
        lampSO.FindProperty("playerController").objectReferenceValue = rig.controller;
        lampSO.FindProperty("playerCamera").objectReferenceValue = rig.camera;
        lampSO.ApplyModifiedPropertiesWithoutUndo();

        // Esconderse con E en los cráteres. Va en el jugador porque es un estado
        // suyo; los puestos concretos los ofrece cada HideSpot.
        var hiding = rig.player.AddComponent<PlayerHiding>();
        var hidingSO = new SerializedObject(hiding);
        hidingSO.FindProperty("playerController").objectReferenceValue = rig.controller;
        hidingSO.FindProperty("playerAnimator").objectReferenceValue = rig.player.GetComponent<PlayerAnimator>();
        hidingSO.FindProperty("controller").objectReferenceValue = rig.player.GetComponent<CharacterController>();
        hidingSO.FindProperty("prompt").objectReferenceValue = Object.FindFirstObjectByType<InteractionPromptUI>();
        hidingSO.FindProperty("playerCamera").objectReferenceValue = rig.camera;
        hidingSO.ApplyModifiedPropertiesWithoutUndo();

        // Ruido del jugador: es lo que la araña escucha. El radar suma al ruido, así
        // que desplegarlo con Tab tiene un costo real y no es información gratis.
        var noise = rig.player.AddComponent<NoiseEmitter>();
        var noiseSO = new SerializedObject(noise);
        noiseSO.FindProperty("playerController").objectReferenceValue = rig.controller;
        noiseSO.FindProperty("radar").objectReferenceValue = radar;
        noiseSO.ApplyModifiedPropertiesWithoutUndo();

        // Bordes oscuros al agacharse, para que se note que estás en sigilo.
        var stealth = hudGO.AddComponent<StealthVignette>();
        var stealthSO = new SerializedObject(stealth);
        stealthSO.FindProperty("playerController").objectReferenceValue = rig.controller;
        stealthSO.FindProperty("volume").objectReferenceValue = globalVolume;
        stealthSO.ApplyModifiedPropertiesWithoutUndo();

        // Coleccionables: las fichas ya no se cablean acá. El spawner las saca de
        // Resources/items.json según el tramo del cuerpo asignado.
        var collectibleMat = BuildCollectibleMaterial();

        // Sin estructuras por ahora: encerraban objetos que después no se podían
        // alcanzar. Los scripts siguen en el proyecto por si volvemos con versiones
        // abiertas (sin techo y con vanos anchos), pero no se instancia ninguna.
        // Props del pack: árboles, plantas, rocas, piedras, cristales y la antena.
        // Se arma antes que el spawner porque el spawner lo referencia.
        var propsGO = new GameObject("PropScatter");
        var propScatter = propsGO.AddComponent<PropScatter>();
        var propGroups = PropLibrary.BuildGroups(out var propMat);

        var propsSO = new SerializedObject(propScatter);
        propsSO.FindProperty("propMaterial").objectReferenceValue = propMat;

        var groupsProp = propsSO.FindProperty("groups");
        groupsProp.arraySize = propGroups.Length;
        for (int i = 0; i < propGroups.Length; i++)
        {
            var g = propGroups[i];
            var element = groupsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("nombre").stringValue = g.nombre;
            element.FindPropertyRelative("peso").floatValue = g.peso;
            element.FindPropertyRelative("celdasPorProp").floatValue = g.celdasPorProp;
            element.FindPropertyRelative("escala").vector2Value = g.escala;
            element.FindPropertyRelative("inclinacion").floatValue = g.inclinacion;
            element.FindPropertyRelative("bloquea").boolValue = g.bloquea;
            element.FindPropertyRelative("separacion").floatValue = g.separacion;

            var prefabsProp = element.FindPropertyRelative("prefabs");
            prefabsProp.arraySize = g.prefabs.Length;
            for (int p = 0; p < g.prefabs.Length; p++)
                prefabsProp.GetArrayElementAtIndex(p).objectReferenceValue = g.prefabs[p];
        }
        propsSO.ApplyModifiedPropertiesWithoutUndo();

        // Cráteres: cobertura contra el oído de la araña. Necesitan capa propia, que
        // hay que registrar en TagManager antes de que nadie la use.
        RegistrarCapaOclusion();

        var wellsGO = new GameObject("Pozos");
        var wells = wellsGO.AddComponent<CoverWell>();
        var wellsSO = new SerializedObject(wells);
        wellsSO.FindProperty("stoneMaterial").objectReferenceValue = BuildRimMaterial(theme.baseColor);
        wellsSO.FindProperty("floorMaterial").objectReferenceValue = BuildCraterFloorMaterial(theme.baseColor);
        wellsSO.FindProperty("markerMaterial").objectReferenceValue = BuildMarkerMaterial();
        wellsSO.ApplyModifiedPropertiesWithoutUndo();

        var spawnerGO = new GameObject("CollectibleSpawner");
        var spawner = spawnerGO.AddComponent<CollectibleSpawner>();
        var spawnerSO = new SerializedObject(spawner);
        spawnerSO.FindProperty("generator").objectReferenceValue = generator;
        spawnerSO.FindProperty("collectibleMaterial").objectReferenceValue = collectibleMat;
        spawnerSO.FindProperty("planetColor").colorValue = theme.baseColor;
        spawnerSO.FindProperty("props").objectReferenceValue = propScatter;
        spawnerSO.FindProperty("wells").objectReferenceValue = wells;
        spawnerSO.ApplyModifiedPropertiesWithoutUndo();

        // Atmósfera propia de cada planeta: cielo y filtro de cámara derivados de la
        // semilla, para que dos generaciones nunca se sientan el mismo lugar.
        var atmosphereGO = new GameObject("PlanetAtmosphere");
        var atmosphere = atmosphereGO.AddComponent<PlanetAtmosphere>();
        var atmosphereSO = new SerializedObject(atmosphere);
        atmosphereSO.FindProperty("generator").objectReferenceValue = generator;
        atmosphereSO.FindProperty("volume").objectReferenceValue = globalVolume;
        atmosphereSO.FindProperty("skyTemplate").objectReferenceValue = skyMaterial;
        atmosphereSO.FindProperty("sunLight").objectReferenceValue = light;
        atmosphereSO.ApplyModifiedPropertiesWithoutUndo();

        // Sin cristales: la ambientación va a venir de los refugios cuando los pongamos.

        // Arañas: población que crece al juntar fichas, siempre fuera de cámara.
        var spiderMat = BuildSpiderMaterial();

        // Ficha de la criatura antes del primer descenso. Necesita el mismo material que
        // las arañas del mundo: lo que ves en la ficha es el bicho real.
        var briefGO = new GameObject("FichasInicio");

        // Música de las fichas. autoPlay en falso: solo suena mientras están abiertas, no
        // durante la partida. La dispara la primera ficha y la apaga la última.
        var briefMusic = briefGO.AddComponent<AmbientMusic>();
        var briefMusicSO = new SerializedObject(briefMusic);
        briefMusicSO.FindProperty("clip").objectReferenceValue = PrepareBriefMusic();
        briefMusicSO.FindProperty("autoPlay").boolValue = false;
        briefMusicSO.FindProperty("fadeInSeconds").floatValue = 2f;
        briefMusicSO.FindProperty("ambientVolume").floatValue = 0.5f;
        briefMusicSO.ApplyModifiedPropertiesWithoutUndo();

        // Los controles se crean primero porque la ficha de la criatura los referencia:
        // al cerrarse, le da paso.
        // El maniquí de la vitrina de controles. Se arma acá y no en la propia pantalla
        // porque la fabrica de rigs es codigo de editor y no existe en la partida.
        //
        // Va en -640 y no en -600, que es donde monta su vitrina la ficha de la criatura:
        // separados, ninguna de las dos camaras puede pillar por error al inquilino de la
        // otra aunque las dos esten vivas a la vez. Queda apagado hasta que se lo pidan.
        var maniqui = PlayerRigFactory.CreateMannequin(new Vector3(0f, -640f, 0f), 9);
        maniqui.transform.SetParent(briefGO.transform, true);

        var controls = briefGO.AddComponent<ControlsBriefUI>();
        var controlsSO = new SerializedObject(controls);
        controlsSO.FindProperty("playerController").objectReferenceValue = rig.controller;
        controlsSO.FindProperty("playerCamera").objectReferenceValue = rig.camera;
        controlsSO.FindProperty("music").objectReferenceValue = briefMusic;
        controlsSO.FindProperty("mannequin").objectReferenceValue = maniqui;
        controlsSO.ApplyModifiedPropertiesWithoutUndo();

        var brief = briefGO.AddComponent<CreatureBriefUI>();
        var briefSO = new SerializedObject(brief);
        briefSO.FindProperty("playerController").objectReferenceValue = rig.controller;
        briefSO.FindProperty("playerCamera").objectReferenceValue = rig.camera;
        briefSO.FindProperty("spiderMaterial").objectReferenceValue = spiderMat;
        briefSO.FindProperty("controls").objectReferenceValue = controls;
        briefSO.FindProperty("music").objectReferenceValue = briefMusic;
        briefSO.ApplyModifiedPropertiesWithoutUndo();
        var spidersGO = new GameObject("SpiderSpawner");
        var spiderSpawner = spidersGO.AddComponent<SpiderSpawner>();
        var spiderSO = new SerializedObject(spiderSpawner);
        spiderSO.FindProperty("generator").objectReferenceValue = generator;
        spiderSO.FindProperty("spiderMaterial").objectReferenceValue = spiderMat;

        // Las cuatro ráfagas de parloteo. En mono a propósito: la fuente es 3D y de este
        // sonido lo que informa es de qué lado viene.
        var voces = spiderSO.FindProperty("chatterClips");
        voces.arraySize = 4;
        for (int i = 0; i < 4; i++)
            voces.GetArrayElementAtIndex(i).objectReferenceValue =
                PrepararEfecto($"Efectos/arana_{i + 1}", "wav");
        spiderSO.FindProperty("player").objectReferenceValue = rig.player.transform;
        spiderSO.FindProperty("viewCamera").objectReferenceValue = rig.camera.GetComponent<Camera>();
        spiderSO.ApplyModifiedPropertiesWithoutUndo();

        // Pausa. Antes ESC solo soltaba el cursor: bajando a un planeta no había forma de
        // salir al menú ni de tocar el volumen sin cerrar el juego a la fuerza.
        var pausaGO = new GameObject("Pausa");
        var pausa = pausaGO.AddComponent<PauseMenuUI>();
        var pausaSO = new SerializedObject(pausa);
        pausaSO.FindProperty("playerController").objectReferenceValue = rig.controller;
        pausaSO.FindProperty("playerCamera").objectReferenceValue = rig.camera;
        pausaSO.FindProperty("onSurface").boolValue = true;
        pausaSO.ApplyModifiedPropertiesWithoutUndo();

        // El cierre de la ruta. Vive apagado en la escena y lo dispara el módulo de
        // ascenso al despegar del último cuerpo.
        var finalGO = new GameObject("CierreDeRuta");
        var finalUI = finalGO.AddComponent<EndingUI>();

        // Módulo de ascenso: el vehículo con el que bajaste, y la única salida. Se
        // coloca solo sobre el suelo asegurado cuando el terreno está listo.
        var moduleGO = new GameObject("ModuloAscenso");
        BuildLanderVisual(moduleGO.transform);

        var module = moduleGO.AddComponent<ExtractionModule>();
        var moduleSO = new SerializedObject(module);
        moduleSO.FindProperty("player").objectReferenceValue = rig.player.transform;
        // El cartel de interacción lo crea CreateGameSystems y no lo devuelve, así que
        // se busca en la escena. Es el mismo que usan las fichas: al pedirlo por
        // cercanía, módulo y fichas se turnan solos sin pisarse.
        moduleSO.FindProperty("prompt").objectReferenceValue =
            Object.FindFirstObjectByType<InteractionPromptUI>();
        moduleSO.FindProperty("generator").objectReferenceValue = generator;
        moduleSO.FindProperty("ending").objectReferenceValue = finalUI;

        // Baliza del módulo: se enciende sola al cerrarse el catálogo.
        //
        // El material de la columna lleva el piso de ambiente al máximo para que brille
        // igual con el sol que tenga el cuerpo. Sin eso, en PRL-000 —cuya estrella está
        // en 0,22— la señal de "por aquí se sale" saldría gris.
        var haz = LanderMaterial("PS1_Baliza", new Color(1f, 0.72f, 0.34f));
        haz.SetFloat("_AmbientFloor", 1f);

        var baliza = moduleGO.AddComponent<ExtractionBeacon>();
        var balizaSO = new SerializedObject(baliza);
        balizaSO.FindProperty("beamMaterial").objectReferenceValue = haz;
        balizaSO.FindProperty("alarmClip").objectReferenceValue = PrepararEfecto("Efectos/alarma", "ogg");
        balizaSO.ApplyModifiedPropertiesWithoutUndo();

        // Música de exploración. Con pausa larga entre pasadas, no en bucle continuo: la
        // mecánica del juego es oír, y música sin parar tapa las pisadas y a la araña
        // justo cuando hay que distinguirlas. Y arranca con retraso para no pisar el
        // aterrizaje ni las fichas de inicio.
        var musicaGO = new GameObject("MusicaSuperficie");
        var musica = musicaGO.AddComponent<AmbientMusic>();
        var musicaSO = new SerializedObject(musica);
        musicaSO.FindProperty("clip").objectReferenceValue = PrepararMusica("Musica/MusicaSuperficie");
        musicaSO.FindProperty("ambientVolume").floatValue = 0.26f;
        musicaSO.FindProperty("fadeInSeconds").floatValue = 7f;
        musicaSO.FindProperty("startDelay").floatValue = 4f;
        musicaSO.FindProperty("loopGapSeconds").floatValue = 28f;
        musicaSO.ApplyModifiedPropertiesWithoutUndo();

        // El radar se creó mucho antes que el módulo, así que la marca de vuelta se
        // cablea acá, cuando el módulo ya existe.
        var radarModuloSO = new SerializedObject(radar);
        radarModuloSO.FindProperty("extraction").objectReferenceValue = moduleGO.transform;
        radarModuloSO.ApplyModifiedPropertiesWithoutUndo();

        // Localizador de la última pieza. En cian, para que no se confunda con la baliza
        // del módulo, que es naranja y diez veces más alta.
        var señal = LanderMaterial("PS1_SenalUltima", new Color(0.66f, 0.94f, 1f));
        señal.SetFloat("_AmbientFloor", 1f);

        var ultimaGO = new GameObject("LocalizadorUltimaFicha");
        var ultima = ultimaGO.AddComponent<LastItemBeacon>();
        var ultimaSO = new SerializedObject(ultima);
        ultimaSO.FindProperty("player").objectReferenceValue = rig.player.transform;
        ultimaSO.FindProperty("pingClip").objectReferenceValue = PrepararEfecto("Efectos/alarma", "ogg");
        ultimaSO.FindProperty("beamMaterial").objectReferenceValue = señal;
        ultimaSO.ApplyModifiedPropertiesWithoutUndo();

        // Rango de uso más ancho: el módulo ahora mide ocho de lado, así que parado
        // junto a una pata sigues estando a cuatro del centro.
        moduleSO.FindProperty("interactRange").floatValue = 6.5f;
        moduleSO.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory("Assets/_Project/Scenes");
        string scenePath = "Assets/_Project/Scenes/Planet_Test_Procedural.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!buildScenes.Any(s => s.path == scenePath))
            buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = buildScenes.ToArray();

        Debug.Log("Planeta procedural construido en " + scenePath);
    }

    // El módulo de ascenso. Todo en coordenadas locales: el objeto raíz se reubica en
    // tiempo de ejecución sobre el suelo asegurado, y los hijos tienen que seguirlo.
    //
    // Se lee como nave a distancia y sin detalle, que es lo que pide la bruma: cuerpo
    // troncocónico, cuatro patas abiertas, una escotilla encendida y una baliza. La luz
    // es lo que de verdad importa — es tu punto de referencia para volver.
    static void BuildLanderVisual(Transform parent)
    {
        var hull = LanderMaterial("PS1_Lander", new Color(0.62f, 0.64f, 0.70f));
        var dark = LanderMaterial("PS1_LanderDark", new Color(0.14f, 0.15f, 0.19f));
        var lit = LanderMaterial("PS1_LanderLit", new Color(0.96f, 0.72f, 0.34f));

        // Escala de nave, no de caseta. Antes medía 3 de alto contra un jugador de 2:
        // apenas metro y medio más que vos, o sea un cobertizo. Ahora ronda los 6, casi
        // el triple que el personaje, que es lo que hace falta para que se lea como el
        // vehículo en el que bajaste.
        AddLocal(parent, PrimitiveType.Cylinder, "Cuerpo",
                 new Vector3(0f, 2.7f, 0f), new Vector3(4.3f, 1.9f, 4.3f), hull);
        AddLocal(parent, PrimitiveType.Cylinder, "Cuello",
                 new Vector3(0f, 5.05f, 0f), new Vector3(2.6f, 0.85f, 2.6f), dark);
        AddLocal(parent, PrimitiveType.Cube, "Escotilla",
                 new Vector3(0f, 2.3f, 2.1f), new Vector3(1.7f, 2.4f, 0.2f), lit);

        // Un anillo de refuerzo a media altura: sin él, el cuerpo agrandado queda como
        // un tarro liso y se le nota que es un cilindro escalado.
        AddLocal(parent, PrimitiveType.Cylinder, "Anillo",
                 new Vector3(0f, 3.6f, 0f), new Vector3(4.6f, 0.16f, 4.6f), dark);

        // Patas: cuatro, en diagonal hacia afuera. Rotadas para que el pie caiga más
        // lejos que la cadera y el conjunto se apoye abierto en vez de recto.
        for (int i = 0; i < 4; i++)
        {
            float angle = 45f + i * 90f;
            var dir = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));

            var leg = AddLocal(parent, PrimitiveType.Cylinder, "Pata" + i,
                               dir * 2.8f + new Vector3(0f, 1.35f, 0f),
                               new Vector3(0.4f, 1.5f, 0.4f), hull);
            leg.transform.localRotation = Quaternion.Euler(dir.z * 22f, 0f, -dir.x * 22f);

            AddLocal(parent, PrimitiveType.Cube, "Pie" + i,
                     dir * 3.9f + new Vector3(0f, 0.2f, 0f),
                     new Vector3(1.3f, 0.34f, 1.3f), dark);
        }

        // Baliza: parpadea a 12 fps como el resto del juego. En la bruma es lo único
        // del módulo que se ve de lejos, así que es lo que te guía de vuelta.
        var beaconGO = new GameObject("Baliza");
        beaconGO.transform.SetParent(parent, false);
        beaconGO.transform.localPosition = new Vector3(0f, 5.9f, 0f);

        var beacon = beaconGO.AddComponent<Light>();
        beacon.type = LightType.Point;
        beacon.color = new Color(1f, 0.66f, 0.30f);
        beacon.range = 34f;
        beacon.intensity = 5.5f;
        beaconGO.AddComponent<NavLightBlink>();

        var hatchGlowGO = new GameObject("LuzEscotilla");
        hatchGlowGO.transform.SetParent(parent, false);
        hatchGlowGO.transform.localPosition = new Vector3(0f, 2.4f, 3.2f);

        var hatchGlow = hatchGlowGO.AddComponent<Light>();
        hatchGlow.type = LightType.Point;
        hatchGlow.color = new Color(1f, 0.78f, 0.45f);
        hatchGlow.range = 13f;
        hatchGlow.intensity = 3.4f;
    }

    static GameObject AddLocal(Transform parent, PrimitiveType type, string name,
                               Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static Material LanderMaterial(string assetName, Color color)
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

    // La capa de oclusión hay que darla de alta en TagManager o Unity la deja sin
    // nombre: funcionar funciona igual, pero en el inspector aparecería como "Layer 8" y
    // nadie sabría para qué es.
    static void RegistrarCapaOclusion()
    {
        RegistrarCapa(CoverWell.Layer, "Ocultacion");

        // Capa de la vitrina de la ficha de criatura: la cámara del mundo no la ve y la
        // de la vitrina no ve nada más.
        RegistrarCapa(9, "Vitrina");
    }

    static void RegistrarCapa(int indice, string nombre)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

        var capas = tagManager.FindProperty("layers");
        if (capas == null || capas.arraySize <= indice) return;

        var capa = capas.GetArrayElementAtIndex(indice);
        if (capa.stringValue == nombre) return;

        capa.stringValue = nombre;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    // El labio: material expulsado del propio suelo, así que sale del color del terreno
    // pero más claro y menos saturado — como roca partida y sin erosionar. Tiene que
    // distinguirse lo justo para leerse de lejos, sin parecer construido.
    static Material BuildRimMaterial(Color planetColor)
    {
        Color.RGBToHSV(planetColor, out float h, out float s, out float v);
        return CraterMaterial("PS1_CraterLabio",
                              Color.HSVToRGB(h, s * 0.55f, Mathf.Clamp01(v * 1.3f + 0.06f)));
    }

    // El fondo, quemado. Bastante más oscuro que el terreno: es lo que hace que el
    // interior se lea como un hoyo y no como piedras puestas en círculo.
    static Material BuildCraterFloorMaterial(Color planetColor)
    {
        Color.RGBToHSV(planetColor, out float h, out float s, out float v);
        return CraterMaterial("PS1_CraterFondo",
                              Color.HSVToRGB(h, s * 0.75f, Mathf.Clamp01(v * 0.42f)));
    }

    static Material CraterMaterial(string assetName, Color color)
    {
        Directory.CreateDirectory("Assets/_Project/Materials");
        AssetDatabase.Refresh();

        string path = $"Assets/_Project/Materials/{assetName}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Cielo Profundo/PS1 Vertex Snap"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_SnapResolution", 140f);
        EditorUtility.SetDirty(mat);

        return mat;
    }

    // Verde, y es el único verde del juego: el cian es interfaz, el ámbar son avisos y
    // balizas, el rojo es daño. Un color que no aparece en ningún otro lado no hay que
    // aprenderlo dos veces.
    // Mismo trato que la música del menú: streaming y Vorbis. Son 22 MB de WAV que sin
    // tocar la importación se irían enteros al build y se cargarían en memoria antes de
    // sonar, para un tema que suena veinte segundos una vez por partida.
    //
    // Este NO hay que cortarlo: viene hecho para repetirse. Medí principio, medio y final
    // y están al mismo nivel, sin desvanecimiento — al revés que el del menú.
    // Un efecto corto: sin comprimir y cargado entero en memoria, igual que las pisadas.
    static AudioClip PrepararEfecto(string relativo, string extension)
    {
        string ruta = $"Assets/_Project/Audio/{relativo}.{extension}";

        var importer = AssetImporter.GetAtPath(ruta) as AudioImporter;
        if (importer == null)
        {
            Debug.LogWarning("No encontré el efecto en " + ruta);
            return null;
        }

        var ajustes = importer.defaultSampleSettings;
        ajustes.loadType = AudioClipLoadType.DecompressOnLoad;
        ajustes.compressionFormat = AudioCompressionFormat.PCM;
        ajustes.preloadAudioData = true;

        importer.defaultSampleSettings = ajustes;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
    }

    // Una pista larga: comprimida y transmitida desde disco, para no meter minutos de
    // audio sin comprimir en memoria.
    static AudioClip PrepararMusica(string relativo)
    {
        string ruta = $"Assets/_Project/Audio/{relativo}.wav";

        var importer = AssetImporter.GetAtPath(ruta) as AudioImporter;
        if (importer == null)
        {
            Debug.LogWarning("No encontré la música en " + ruta);
            return null;
        }

        var ajustes = importer.defaultSampleSettings;
        ajustes.loadType = AudioClipLoadType.Streaming;
        ajustes.compressionFormat = AudioCompressionFormat.Vorbis;
        ajustes.quality = 0.6f;
        ajustes.preloadAudioData = false;

        importer.defaultSampleSettings = ajustes;
        importer.forceToMono = false;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
    }

    static AudioClip PrepareBriefMusic()
    {
        const string ruta = "Assets/_Project/Audio/Musica/MusicaFichas.wav";

        var importer = AssetImporter.GetAtPath(ruta) as AudioImporter;
        if (importer == null)
        {
            Debug.LogWarning("No encontré la música de las fichas en " + ruta);
            return null;
        }

        var ajustes = importer.defaultSampleSettings;
        ajustes.loadType = AudioClipLoadType.Streaming;
        ajustes.compressionFormat = AudioCompressionFormat.Vorbis;
        ajustes.quality = 0.6f;
        ajustes.preloadAudioData = false;

        importer.defaultSampleSettings = ajustes;
        importer.forceToMono = false;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
    }

    static Material BuildMarkerMaterial()
    {
        Directory.CreateDirectory("Assets/_Project/Materials");
        AssetDatabase.Refresh();

        const string path = "Assets/_Project/Materials/PS1_Escondite.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Cielo Profundo/PS1 Vertex Snap"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", new Color(0.30f, 0.88f, 0.60f));
        mat.SetFloat("_SnapResolution", 140f);
        // Piso de ambiente alto: el poste tiene que verse aunque el cuerpo sea oscurísimo.
        mat.SetFloat("_AmbientFloor", 0.75f);
        EditorUtility.SetDirty(mat);

        return mat;
    }

    static Material BuildSpiderMaterial()
    {
        Directory.CreateDirectory("Assets/_Project/Materials");
        AssetDatabase.Refresh();

        string path = "Assets/_Project/Materials/PS1_Spider.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Cielo Profundo/PS1 Vertex Snap");
        var mat = new Material(shader);
        // Casi negra: en la bruma tiene que leerse como silueta, no como criatura detallada.
        mat.SetColor("_BaseColor", new Color(0.075f, 0.06f, 0.1f));
        mat.SetFloat("_SnapResolution", 140f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Material BuildCollectibleMaterial()
    {
        Directory.CreateDirectory("Assets/_Project/Materials");
        AssetDatabase.Refresh();

        string path = "Assets/_Project/Materials/PS1_Collectible.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Cielo Profundo/PS1 Vertex Snap");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.918f, 0.949f, 1f));
        mat.SetFloat("_SnapResolution", 140f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
