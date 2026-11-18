using UnityEngine;
using UnityEngine.UI;

// Ficha de criatura antes de pisar la superficie. Negro, texto blanco, y la araña de
// verdad girando al costado. Enter para bajar.
//
// Existe porque la mecánica central del bicho —es ciega, no sorda— no se puede deducir
// jugando. Si nadie te lo dice, morir detrás de un muro se lee como que el juego está
// roto, no como que hiciste ruido.
//
// Solo la primera vez. Es información, no un peaje: una vez que la sabes, repetirla en
// cada descenso sería castigar al que ya entendió.
//
// La araña no es una imagen: es una instancia real puesta lejos del mundo, filmada por
// su propia cámara sobre una textura. Es la forma estándar de meter un modelo 3D dentro
// de la interfaz, y acá además garantiza que lo que ves en la ficha es exactamente el
// bicho que te va a cazar, con sus mismas patas y su mismo andar.
public class CreatureBriefUI : MonoBehaviour
{
    [SerializeField] PlayerController playerController;
    [SerializeField] PlayerCamera playerCamera;
    [SerializeField] Material spiderMaterial;

    [Tooltip("La pantalla de controles, que va justo después de esta.")]
    [SerializeField] ControlsBriefUI controls;

    [Tooltip("Música de las fichas. Arranca con esta pantalla y se va cuando se cierra la última.")]
    [SerializeField] AmbientMusic music;

    [Tooltip("Capa exclusiva de la vitrina. La cámara del mundo no la ve y la de la vitrina no ve nada más.")]
    [SerializeField] int showcaseLayer = 9;

    [Tooltip("Dónde se monta la vitrina, bien lejos del terreno para que no se cuele nada en el encuadre.")]
    [SerializeField] Vector3 showcasePosition = new Vector3(0f, -600f, 0f);

    [SerializeField] float spinSpeed = 26f;

    GameObject root;
    Transform showcaseSpider;
    Camera showcaseCamera;
    RenderTexture texture;
    Vector3 spinPivot;
    bool shown;
    bool framed;

    void Start()
    {
        // Repetirla en cada bajada sería un peaje. Se muestra una vez por partida.
        if (Expedition.CreatureBriefSeen)
        {
            // Aunque esta ya se haya visto, la de controles puede no haberse visto: son
            // dos claves distintas y hay que dejarle paso igual.
            controls?.Show();

            // Solo si la de controles se va a mostrar de verdad: arrancar la música para
            // nadie dejaría un tema sonando encima de la partida ya empezada.
            if (controls != null && controls.IsShowing) music?.Play();
            enabled = false;
            return;
        }

        BuildShowcase();
        BuildUI();
        Show();
        music?.Play();
    }

    void Update()
    {
        if (!shown) return;

        if (!framed && showcaseSpider != null)
        {
            framed = true;
            FrameShowcase();
        }

        if (showcaseSpider != null)
            showcaseSpider.RotateAround(spinPivot, Vector3.up, spinSpeed * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
            Hide();
    }

    void Show()
    {
        shown = true;
        root.SetActive(true);

        if (playerController != null) playerController.enabled = false;
        if (playerCamera != null) playerCamera.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Hide()
    {
        shown = false;
        root.SetActive(false);

        Expedition.CreatureBriefSeen = true;

        if (playerController != null) playerController.enabled = true;
        if (playerCamera != null) playerCamera.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // La vitrina entera se desmonta: cámara, araña y textura. Dejarla viva costaría
        // un render extra por cuadro durante toda la partida para no mostrar nada.
        if (showcaseCamera != null) Destroy(showcaseCamera.gameObject);
        if (showcaseSpider != null) Destroy(showcaseSpider.gameObject);
        if (texture != null) { texture.Release(); Destroy(texture); }

        UISound.Click();
        enabled = false;

        // Encadenada: los controles entran en cuanto se cierra esta.
        controls?.Show();
    }

    // ── La vitrina ──────────────────────────────────────────────────────────────

    void BuildShowcase()
    {
        texture = new RenderTexture(512, 512, 16) { antiAliasing = 1 };

        var camGO = new GameObject("CamaraVitrina");
        camGO.transform.position = showcasePosition + new Vector3(0f, 1.6f, -7.5f);
        camGO.transform.rotation = Quaternion.Euler(8f, 0f, 0f);

        showcaseCamera = camGO.AddComponent<Camera>();
        showcaseCamera.targetTexture = texture;
        showcaseCamera.cullingMask = 1 << showcaseLayer;
        showcaseCamera.clearFlags = CameraClearFlags.SolidColor;
        showcaseCamera.backgroundColor = Color.clear;
        showcaseCamera.fieldOfView = 40f;
        showcaseCamera.nearClipPlane = 0.1f;

        // SIN luz propia, y esto es importante.
        //
        // El shader PS1 usa GetMainLight(), y URP no respeta máscaras de capa en la luz
        // principal: elige la direccional más brillante de TODA la escena. Una luz de
        // vitrina a intensidad 2,4 le ganaba el puesto al sol del planeta en varios
        // cuerpos —van de 0,22 a 2,2— y le cambiaba la iluminación al mundo entero
        // mientras la ficha estaba abierta. Y donde no le ganaba, era el sol naranja del
        // planeta el que teñía la araña de marrón.
        //
        // En vez de pelear con eso, la araña de la ficha usa su propio material, más
        // claro, y se lee sola con la luz que haya. Cero luces nuevas, cero conflicto, y
        // predecible en los doce cuerpos.
        var go = new GameObject("ArañaVitrina");
        go.SetActive(false);
        go.transform.position = showcasePosition;

        var spider = go.AddComponent<SpiderCreature>();
        spider.SetMaterial(BuildShowcaseMaterial());

        // Congelada: no avanza ni caza, pero las patas siguen acomodándose. Eso es lo
        // que hace que en la ficha se vea viva y no como un maniquí.
        spider.FrozenByCinematic = true;

        go.SetActive(true);
        SetLayerRecursive(go, showcaseLayer);

        showcaseSpider = go.transform;
    }

    // Copia del material de la araña, más clara. En el mundo es casi negra porque tiene
    // que leerse como silueta en la niebla; en la ficha la estás mirando de cerca y hay
    // que verle las patas.
    Material BuildShowcaseMaterial()
    {
        var shader = spiderMaterial != null
            ? spiderMaterial.shader
            : Shader.Find("Cielo Profundo/PS1 Vertex Snap");

        var mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.46f, 0.44f, 0.52f));
        mat.SetFloat("_SnapResolution", 140f);

        // Piso de ambiente alto: es lo que garantiza que se vea aunque el sol del cuerpo
        // que estás visitando sea flojísimo, como en PRL-000.
        mat.SetFloat("_AmbientFloor", 0.62f);
        return mat;
    }

    // Encuadre medido, no adivinado. Se hace en el primer Update y no al construir
    // porque las patas las coloca la cinemática inversa en su primer cuadro: midiendo
    // antes, los límites saldrían de una pose sin resolver.
    void FrameShowcase()
    {
        var renderers = showcaseSpider.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        // Distancia para que una esfera del tamaño del bicho entre en el campo de visión.
        // El margen deja aire alrededor: pegado al borde se ve apretado.
        float radius = bounds.extents.magnitude;
        float distance = radius / Mathf.Sin(showcaseCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;

        showcaseCamera.transform.position = bounds.center + new Vector3(0f, radius * 0.2f, -distance);
        showcaseCamera.transform.LookAt(bounds.center);

        // El giro va alrededor del centro real de la figura, no del origen del objeto:
        // el origen está en el cuerpo y las patas cuelgan, así que girando sobre él la
        // araña describía un círculo en vez de girar sobre sí misma.
        spinPivot = bounds.center;
    }

    // Recursivo porque el cuerpo y las ocho patas se construyen como hijos: poner la
    // capa solo en la raíz dejaría a la araña invisible para la cámara de la vitrina.
    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
    }

    // ── La ficha ────────────────────────────────────────────────────────────────

    void BuildUI()
    {
        root = new GameObject("CanvasFicha");
        root.transform.SetParent(transform, false);
        root.SetActive(false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Por encima del fundido de entrada, que está en 200: si no, el planeta se
        // abriría desde negro por delante de la ficha.
        canvas.sortingOrder = 210;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var fondo = new GameObject("Fondo");
        fondo.transform.SetParent(root.transform, false);
        var fondoImg = fondo.AddComponent<Image>();
        fondoImg.color = Color.black;
        fondoImg.raycastTarget = false;
        HoloUI.Stretch(fondo.GetComponent<RectTransform>());

        // Columna de texto a la izquierda.
        var titulo = HoloUI.CreateText(root.transform, "Titulo", 46, TextAnchor.UpperLeft, Color.white);
        Place(titulo.rectTransform, new Vector2(180f, -240f), new Vector2(820f, 60f));
        titulo.text = HoloUI.Espaciar("NO VE");

        var cuerpo = HoloUI.CreateText(root.transform, "Cuerpo", 20, TextAnchor.UpperLeft,
                                       new Color(0.82f, 0.84f, 0.88f));
        Place(cuerpo.rectTransform, new Vector2(182f, -330f), new Vector2(880f, 440f));
        cuerpo.lineSpacing = 1.45f;

        // Sin truncar y sin saltos de línea a mano dentro de los párrafos. Los saltos
        // manuales suponen un ancho concreto, y como el componente TAMBIÉN ajusta solo,
        // cada renglón se partía dos veces y quedaban palabras huérfanas colgando. Y con
        // el truncado vertical por defecto, las líneas de más se caían fuera de la caja:
        // por eso faltaba la última frase. Los únicos saltos que quedan separan párrafos.
        cuerpo.verticalOverflow = VerticalWrapMode.Overflow;
        cuerpo.text =
            "No tiene ojos. Puedes cruzarte con ella a plena luz y no te ve.\n\n" +
            "Oye. Caminar la trae, correr la trae desde mucho más lejos, y el radar " +
            "desplegado la trae aunque estés quieto.\n\n" +
            "Agachado casi no haces ruido. Y dentro de un pozo estás a salvo: puede " +
            "rondar el brocal, pero ahí no te alcanza.\n\n" +
            "La barra de la esquina te dice cuánto estás haciendo.";

        var pie = HoloUI.CreateText(root.transform, "Pie", 16, TextAnchor.UpperLeft,
                                    new Color(0.55f, 0.58f, 0.64f));
        Place(pie.rectTransform, new Vector2(182f, -800f), new Vector2(700f, 30f));
        pie.text = "ENTER   descender";

        // La vitrina a la derecha.
        var visor = new GameObject("Vitrina");
        visor.transform.SetParent(root.transform, false);
        var raw = visor.AddComponent<RawImage>();
        raw.texture = texture;
        raw.raycastTarget = false;

        var visorRT = visor.GetComponent<RectTransform>();
        visorRT.anchorMin = visorRT.anchorMax = new Vector2(1f, 0.5f);
        visorRT.pivot = new Vector2(1f, 0.5f);
        visorRT.sizeDelta = new Vector2(560f, 560f);
        visorRT.anchoredPosition = new Vector2(-200f, 0f);
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }
}
