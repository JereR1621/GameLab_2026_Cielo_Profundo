using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Los controles, en el mismo formato negro que la ficha de la criatura. Aparece justo
// después de ella, antes del primer descenso.
//
// La lista está sacada del código, tecla por tecla, no de memoria. Una pantalla de
// controles que miente es peor que no tenerla: el jugador prueba lo que dice, no le
// responde, y a partir de ahí desconfía de todo lo demás que lea.
//
// Al costado hay un maniquí que va alternando entre caminar, correr, agacharse y
// saltar, y la fila de la tecla correspondiente se enciende al mismo tiempo. Leer
// "CTRL agacharse" no dice cuánto te encoges ni que la postura es la que te calla;
// verlo sí. Y como el maniquí usa el mismo PlayerAnimator que el jugador, no puede
// mostrar algo distinto de lo que va a pasar cuando aprietes la tecla.
//
// Solo la primera vez, como la ficha. Y se puede volver a ver desde el menú de pruebas.
public class ControlsBriefUI : MonoBehaviour
{
    [SerializeField] PlayerController playerController;
    [SerializeField] PlayerCamera playerCamera;

    [Tooltip("Música de las fichas. Esta pantalla es la última, así que es la que la apaga.")]
    [SerializeField] AmbientMusic music;

    [Tooltip("Cuánto tarda en irse la música al cerrar. Larga: se solapa con el fundido a la superficie en vez de cortarse.")]
    [SerializeField] float musicFadeOut = 2.5f;

    [Header("Vitrina")]
    [Tooltip("Maniquí guardado apagado en la escena. Lo arma el constructor porque la fábrica de rigs es de editor.")]
    [SerializeField] GameObject mannequin;

    [Tooltip("Capa exclusiva de la vitrina. La cámara del mundo no la ve y la de la vitrina no ve nada más.")]
    [SerializeField] int showcaseLayer = 9;

    [Tooltip("Hacia dónde mira el maniquí. Tres cuartos: de frente no se le ve la zancada y de perfil no se le ve el traje.")]
    [SerializeField] float mannequinYaw = 215f;

    [Tooltip("Altura del salto de la demostración.")]
    [SerializeField] float jumpHeight = 1.15f;

    GameObject root;
    bool shown;

    // Cuadro en que se abrió. La misma pulsación de Enter que cierra la ficha de la
    // criatura llega hasta acá —las dos pantallas corren en el mismo cuadro y el orden
    // entre componentes no está garantizado— y cerraba esta al instante, así que del
    // monstruo se pasaba directo al descenso. Es el mismo resguardo que llevan los
    // paneles de la nave y el escondite.
    int shownFrame = -1;

    public bool IsShowing => shown;

    // ── La demostración ─────────────────────────────────────────────────────────

    enum Pose { Caminar, Correr, Agachado, Salto }

    struct Paso
    {
        public Pose pose;
        public string tecla;      // qué fila se enciende
        public string leyenda;    // qué se lee bajo el maniquí
        public float duracion;
    }

    // Orden pensado para que se note el contraste: caminar y correr seguidos hacen
    // evidente cuánto crece la zancada, y agachado justo después deja ver lo bajo que
    // te pones. El salto va al final porque es el único que rompe el ciclo de pasos.
    static readonly Paso[] Ciclo =
    {
        new Paso { pose = Pose.Caminar,  tecla = "W A S D", leyenda = "caminando", duracion = 2.6f },
        new Paso { pose = Pose.Correr,   tecla = "SHIFT",   leyenda = "corriendo", duracion = 2.4f },
        new Paso { pose = Pose.Agachado, tecla = "CTRL",    leyenda = "agachado",  duracion = 2.6f },
        new Paso { pose = Pose.Salto,    tecla = "ESPACIO", leyenda = "saltando",  duracion = 1.7f },
    };

    int paso;
    float pasoTiempo;

    PlayerAnimator maniqui;
    Vector3 maniquiBase;
    Camera showcaseCamera;
    RenderTexture texture;
    Text leyenda;
    bool encuadrado;

    // Los materiales del traje se aclaran mientras dura la pantalla, y hay que poder
    // devolverlos: son los mismos objetos que usa el jugador de verdad.
    readonly List<(MeshRenderer renderer, Material original)> materialesOriginales =
        new List<(MeshRenderer, Material)>();
    readonly List<Material> copias = new List<Material>();

    // Filas por tecla, para encenderlas cuando toca.
    readonly Dictionary<string, (Text tecla, Text accion)> filas =
        new Dictionary<string, (Text, Text)>();

    static readonly Color TeclaApagada = new Color(0.88f, 0.90f, 0.94f);
    static readonly Color TeclaEncendida = new Color(0.96f, 0.58f, 0.32f);
    static readonly Color AccionApagada = new Color(0.70f, 0.73f, 0.79f);
    static readonly Color AccionEncendida = Color.white;

    // ── Las filas ───────────────────────────────────────────────────────────────

    static readonly (string tecla, string accion)[] Movimiento =
    {
        ("W A S D", "moverse"),
        ("RATÓN", "mirar"),
        ("SHIFT", "correr — gasta aguante y te delata mucho más"),
        ("CTRL", "agacharse — casi no haces ruido"),
        ("ESPACIO", "saltar, y mantener para planear al caer"),
    };

    static readonly (string tecla, string accion)[] Acciones =
    {
        ("E", "recoger fichas, usar consolas, esconderse y salir"),
        ("E repetido", "zafarse cuando la araña te atrapa"),
        ("F", "lámpara de casco"),
        ("TAB", "radar — te delata mientras esté desplegado"),
        ("C", "catálogo de fichas"),
    };

    static readonly (string tecla, string accion)[] Bengala =
    {
        ("BOTÓN DERECHO", "apuntar"),
        ("BOTÓN IZQUIERDO", "disparar mientras apuntas"),
    };

    public void Show()
    {
        if (Expedition.ControlsBriefSeen)
        {
            enabled = false;
            return;
        }

        BuildShowcase();
        BuildUI();

        shown = true;
        shownFrame = Time.frameCount;
        root.SetActive(true);

        paso = 0;
        pasoTiempo = 0f;
        AplicarPaso();

        if (playerController != null) playerController.enabled = false;
        if (playerCamera != null) playerCamera.enabled = false;
    }

    void Update()
    {
        if (!shown) return;

        AvanzarDemostracion();

        if (Time.frameCount == shownFrame) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
            Hide();
    }

    void AvanzarDemostracion()
    {
        if (maniqui == null) return;

        // El encuadre se mide en el primer cuadro con la pose ya puesta, no al
        // construir: recién ahí las piernas están donde van, y midiendo antes los
        // límites saldrían de un maniquí en posición de reposo.
        if (!encuadrado)
        {
            encuadrado = true;
            Encuadrar();
        }

        pasoTiempo += Time.deltaTime;

        // El salto no es una pose fija: el maniquí sube y baja de verdad. Una pose de
        // caída sostenida dos segundos se lee como flotar, no como saltar.
        if (Ciclo[paso].pose == Pose.Salto)
        {
            float t = Mathf.Clamp01(pasoTiempo / (Ciclo[paso].duracion * 0.72f));
            float altura = Mathf.Sin(t * Mathf.PI) * jumpHeight;
            maniqui.transform.position = maniquiBase + Vector3.up * altura;

            // Despegado del suelo va la pose de aire; apoyado, la de reposo. El cuarto
            // final del paso lo pasa de pie: es lo que hace que se lea la caída.
            maniqui.ShowcaseAirborne = altura > 0.03f;
        }

        if (pasoTiempo < Ciclo[paso].duracion) return;

        pasoTiempo = 0f;
        paso = (paso + 1) % Ciclo.Length;
        AplicarPaso();
    }

    void AplicarPaso()
    {
        if (maniqui == null) return;

        var p = Ciclo[paso];

        maniqui.transform.position = maniquiBase;
        maniqui.ShowcaseAirborne = false;
        maniqui.ShowcaseCrouch = p.pose == Pose.Agachado;
        maniqui.ShowcaseRun = p.pose == Pose.Correr;

        // Velocidades tomadas de las del jugador, no inventadas: la zancada del animador
        // se escala con la velocidad, así que con números de fantasía el maniquí correría
        // con un tranco que en el juego no existe.
        switch (p.pose)
        {
            case Pose.Caminar: maniqui.ShowcaseSpeed = 3.4f; break;
            case Pose.Correr: maniqui.ShowcaseSpeed = 6.2f; break;
            case Pose.Agachado: maniqui.ShowcaseSpeed = 1.9f; break;
            default: maniqui.ShowcaseSpeed = 0f; break;
        }

        if (leyenda != null) leyenda.text = p.leyenda;

        foreach (var kv in filas)
        {
            bool activa = kv.Key == p.tecla;
            kv.Value.tecla.color = activa ? TeclaEncendida : TeclaApagada;
            kv.Value.accion.color = activa ? AccionEncendida : AccionApagada;
        }
    }

    void Hide()
    {
        shown = false;
        root.SetActive(false);
        Expedition.ControlsBriefSeen = true;

        // La música se va con la pantalla: es de las fichas, no del planeta.
        music?.FadeOut(musicFadeOut);

        DesmontarVitrina();

        if (playerController != null) playerController.enabled = true;
        if (playerCamera != null) playerCamera.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        UISound.Click();
        enabled = false;
    }

    // ── La vitrina ──────────────────────────────────────────────────────────────

    void BuildShowcase()
    {
        if (mannequin == null)
        {
            Debug.LogWarning("ControlsBriefUI: sin maniquí asignado; la pantalla sale sin vitrina.");
            return;
        }

        texture = new RenderTexture(512, 640, 16) { antiAliasing = 1 };

        var camGO = new GameObject("CamaraControles");
        showcaseCamera = camGO.AddComponent<Camera>();
        showcaseCamera.targetTexture = texture;
        showcaseCamera.cullingMask = 1 << showcaseLayer;
        showcaseCamera.clearFlags = CameraClearFlags.SolidColor;
        showcaseCamera.backgroundColor = Color.clear;
        showcaseCamera.fieldOfView = 40f;
        showcaseCamera.nearClipPlane = 0.1f;

        mannequin.SetActive(true);
        mannequin.transform.rotation = Quaternion.Euler(0f, mannequinYaw, 0f);
        maniquiBase = mannequin.transform.position;

        maniqui = mannequin.GetComponent<PlayerAnimator>();
        if (maniqui != null) maniqui.ShowcaseMode = true;

        Aclarar();
        encuadrado = false;
    }

    // Mismo problema que tenía la araña de la ficha: el shader PS1 se ilumina con la luz
    // principal de la escena, y URP no respeta máscaras de capa para elegirla. O sea que
    // al maniquí lo alumbra el sol del cuerpo que estés visitando, y esos van de 0,22 a
    // 2,2 de intensidad: en PRL-000 saldría una silueta negra.
    //
    // La solución de allá sirve acá y no toca ninguna luz: se sube el piso de ambiente
    // en copias de los materiales del traje. Copias, porque son los materiales del
    // jugador de verdad y pintarlos sería repintarlo a él también.
    void Aclarar()
    {
        var porOriginal = new Dictionary<Material, Material>();

        foreach (var r in mannequin.GetComponentsInChildren<MeshRenderer>(true))
        {
            var original = r.sharedMaterial;
            if (original == null) continue;

            Material copia;
            if (!porOriginal.TryGetValue(original, out copia))
            {
                copia = new Material(original);
                if (copia.HasProperty("_AmbientFloor")) copia.SetFloat("_AmbientFloor", 0.70f);
                porOriginal[original] = copia;
                copias.Add(copia);
            }

            materialesOriginales.Add((r, original));

            // sharedMaterial y no material: asignar material instanciaría otra copia por
            // cada uno de los cuarenta y pico trozos del traje.
            r.sharedMaterial = copia;
        }
    }

    void Encuadrar()
    {
        var renderers = mannequin.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        float radius = bounds.extents.magnitude;

        // Margen generoso: el encuadre se mide de pie, pero después el maniquí salta más
        // de un metro. Medido justo, en el salto se le iría la cabeza fuera del cuadro.
        float distance = radius / Mathf.Sin(showcaseCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.7f;

        Vector3 centro = bounds.center + Vector3.up * jumpHeight * 0.35f;
        showcaseCamera.transform.position = centro + new Vector3(0f, 0f, -distance);
        showcaseCamera.transform.LookAt(centro);
    }

    void DesmontarVitrina()
    {
        // Los materiales vuelven a los originales ANTES de destruir las copias. Al revés
        // dejaría a los renderers apuntando a materiales destruidos, y aunque el maniquí
        // se apague, esa es la clase de cosa que reaparece como rosa fluorescente el día
        // que alguien lo vuelva a encender.
        foreach (var par in materialesOriginales)
            if (par.renderer != null) par.renderer.sharedMaterial = par.original;
        materialesOriginales.Clear();

        foreach (var m in copias) if (m != null) Destroy(m);
        copias.Clear();

        if (maniqui != null) maniqui.ShowcaseMode = false;
        if (mannequin != null)
        {
            mannequin.transform.position = maniquiBase;
            mannequin.SetActive(false);
        }

        if (showcaseCamera != null) Destroy(showcaseCamera.gameObject);
        if (texture != null) { texture.Release(); Destroy(texture); }

        showcaseCamera = null;
        texture = null;
        maniqui = null;
    }

    // ── La pantalla ─────────────────────────────────────────────────────────────

    void BuildUI()
    {
        root = new GameObject("CanvasControles");
        root.transform.SetParent(transform, false);
        root.SetActive(false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 211;   // por encima de la ficha de criatura

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

        var titulo = HoloUI.CreateText(root.transform, "Titulo", 46, TextAnchor.UpperLeft, Color.white);
        Place(titulo.rectTransform, new Vector2(180f, -150f), new Vector2(900f, 60f));
        titulo.text = HoloUI.Espaciar("CONTROLES");

        float y = -250f;
        y = Bloque(root.transform, "MOVERSE", Movimiento, y);
        y = Bloque(root.transform, "EN LA SUPERFICIE", Acciones, y);
        y = Bloque(root.transform, "BENGALA", Bengala, y);

        var pie = HoloUI.CreateText(root.transform, "Pie", 16, TextAnchor.UpperLeft,
                                    new Color(0.55f, 0.58f, 0.64f));
        Place(pie.rectTransform, new Vector2(182f, y - 26f), new Vector2(700f, 30f));
        pie.text = "ENTER   descender";

        if (texture != null) BuildVitrina();
    }

    void BuildVitrina()
    {
        var visor = new GameObject("Vitrina");
        visor.transform.SetParent(root.transform, false);
        var raw = visor.AddComponent<RawImage>();
        raw.texture = texture;
        raw.raycastTarget = false;

        // Anclada a la derecha y centrada en vertical. La columna de acciones llega hasta
        // 1170 de las 1920 de referencia y esto arranca en 1330: quedan 160 de aire
        // aunque la fila más larga se estire.
        var rt = visor.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(440f, 550f);
        rt.anchoredPosition = new Vector2(-150f, 10f);

        // La leyenda va colgada de la vitrina y no del lienzo: así sigue debajo del
        // maniquí sin que haya que recalcular nada si la vitrina cambia de sitio.
        leyenda = HoloUI.CreateText(visor.transform, "Leyenda", 18, TextAnchor.UpperCenter,
                                    new Color(0.96f, 0.58f, 0.32f));
        var lrt = leyenda.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 0f);
        lrt.pivot = new Vector2(0.5f, 1f);
        lrt.sizeDelta = new Vector2(0f, 28f);
        lrt.anchoredPosition = new Vector2(0f, -6f);
    }

    // Devuelve dónde quedó el borde inferior, para que el siguiente bloque se apoye ahí
    // en vez de tener yo que ir sumando posiciones a mano y que se solapen al cambiar
    // cualquier fila.
    float Bloque(Transform parent, string titulo, (string tecla, string accion)[] filasBloque, float y)
    {
        var cabecera = HoloUI.CreateText(parent, "Cab_" + titulo, 15, TextAnchor.UpperLeft,
                                         new Color(0.96f, 0.58f, 0.32f));
        Place(cabecera.rectTransform, new Vector2(182f, y), new Vector2(700f, 22f));
        cabecera.text = titulo;
        y -= 34f;

        foreach (var fila in filasBloque)
        {
            var izquierda = HoloUI.CreateText(parent, "Tecla", 17, TextAnchor.UpperLeft, TeclaApagada);
            Place(izquierda.rectTransform, new Vector2(182f, y), new Vector2(280f, 26f));
            izquierda.text = fila.tecla;

            var derecha = HoloUI.CreateText(parent, "Accion", 17, TextAnchor.UpperLeft, AccionApagada);
            Place(derecha.rectTransform, new Vector2(470f, y), new Vector2(700f, 26f));
            derecha.text = fila.accion;

            filas[fila.tecla] = (izquierda, derecha);
            y -= 30f;
        }

        return y - 18f;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }
}
