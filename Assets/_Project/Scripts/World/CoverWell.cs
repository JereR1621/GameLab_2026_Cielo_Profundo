using System.Collections.Generic;
using UnityEngine;

// Pozos de piedra seca que sirven de refugio. Reemplazan a los cráteres.
//
// Un brocal de piedra apilada, compacto y más ancho que alto: anillo cerrado, sin
// entrada, con la boca oscura en el medio. Se entra saltándolo y se sale saltándolo.
//
// El cambio no es solo estético. Los cráteres tenían dos problemas de fondo que este
// planteamiento no tiene:
//
//   · Un cuenco por el que se pueda subir no puede ser hondo —el jugador no trepa más de
//     45 grados—, así que siempre salía un plato en vez de un pozo. El brocal resuelve la
//     tapadera hacia ARRIBA, con muro, en vez de hacia abajo con profundidad.
//
//   · Excavar la malla del terreno para hacer el hoyo pedía una resolución que el terreno
//     no tiene: las celdas miden tres unidades y el interior de esto mide dos. Acá no se
//     excava nada. Te escondes de pie, a ras de suelo, y lo que te tapa es la pared.
//
// Y encaja mejor con la ficción, que es lo que más me convence: hubo una civilización
// antes que tú. Un cráter es algo que le pasó al planeta; un pozo de piedra es algo que
// alguien construyó, y eso es exactamente lo que el juego dice que vas a encontrar.
//
// No van pegados a la ficha: van cerca. Uno encima del objeto sería un botón; a doce o
// veinte unidades hay que decidir si corres hasta él o aguantas donde estás.
public class CoverWell : MonoBehaviour
{
    // Capa propia. Si la oclusión chocara contra todo, cada árbol y cada roca del
    // scatter te escondería y el mapa entero sería cobertura.
    public const int Layer = 8;
    public static int Mask => 1 << Layer;

    // Dónde quedó cada pozo y cuánto ocupa. Lo consulta la araña para no meterse dentro.
    //
    // Hace falta una lista y no un collider porque la araña no usa física: se mueve
    // sumando a su posición, sin comprobar nada. Es lo correcto para un bicho de patas
    // procedurales —una cápsula le pelearía a la cinemática inversa— pero significa que
    // atraviesa cualquier pared, y una araña metiéndose por la piedra hasta el fondo del
    // pozo deja la mecánica de esconderse en ridículo.
    public static readonly List<(Vector3 centro, float radio)> Todos =
        new List<(Vector3, float)>();

    [SerializeField] Material stoneMaterial;
    [SerializeField] Material floorMaterial;

    [Tooltip("Material del poste que marca el refugio. Verde, el único del juego.")]
    [SerializeField] Material markerMaterial;

    [Header("Cantidad")]
    [Tooltip("Cuántas fichas tienen un pozo cerca. Una fracción: con 0.7, siete de cada diez.")]
    [SerializeField, Range(0f, 1f)] float fractionOfItems = 0.7f;

    [Tooltip("A qué distancia de la ficha se levanta el pozo. Lejos a propósito: pegado al objeto sería un botón de seguridad, no una decisión.")]
    [SerializeField] Vector2 distanceFromItem = new Vector2(12f, 20f);

    [Header("Medidas del brocal")]
    [Tooltip("Radio exterior. Compacto: el interior tiene que dar para el jugador y para nada más.")]
    [SerializeField] Vector2 outerRadius = new Vector2(1.45f, 1.65f);

    [Tooltip("Grosor de la pared. Gruesa, como en la piedra seca de verdad, que se sostiene por peso y no por argamasa.")]
    [SerializeField] Vector2 wallThickness = new Vector2(0.44f, 0.54f);

    [Tooltip("Alto sobre el suelo. Cerca del metro: a la altura del hombro, que es lo que deja asomar la cabeza apoyando las manos en el borde.")]
    [SerializeField] Vector2 wallHeight = new Vector2(0.95f, 1.15f);

    [Header("Aparejo")]
    [Tooltip("Hiladas de piedra. Cada una va corrida respecto de la anterior, como en un muro de verdad.")]
    [SerializeField] Vector2Int courses = new Vector2Int(5, 7);

    [Tooltip("Ancho aproximado de cada piedra. De él sale cuántas entran en cada vuelta.")]
    [SerializeField] float stoneWidth = 0.42f;

    [Tooltip("Cuánto varía cada piedra respecto de su hueco. Sin esto el muro es una rejilla.")]
    [SerializeField, Range(0f, 0.5f)] float stoneJitter = 0.22f;

    [Tooltip("Cuánto asoman o se hunden las piedras en la pared. Es lo que le da el relieve irregular.")]
    [SerializeField] float reliefDepth = 0.05f;

    [Header("Coronación")]
    [Tooltip("Losas planas de encima. Sobresalen un poco de la pared, como en la foto.")]
    [SerializeField] Vector2Int capStones = new Vector2Int(11, 15);

    [SerializeField] float capOverhang = 0.07f;
    [SerializeField] Vector2 capHeight = new Vector2(0.10f, 0.17f);

    [Header("Oclusión")]
    [Tooltip("Cuántos volúmenes invisibles forman el anillo que tapa el sonido. Se solapan entre sí para que no queden rendijas.")]
    [SerializeField] int occluderSegments = 12;

    [Tooltip("Cuánto baja el volumen de oclusión por debajo del suelo, para que la línea hacia la araña no pase por abajo.")]
    [SerializeField] float occluderSink = 0.3f;

    [SerializeField] float groundTolerance = 2f;

    public void BuildAround(IReadOnlyList<Vector3> itemPositions, IReadOnlyList<Vector3> walkableCells,
                            Vector3 spawnPoint)
    {
        // La lista es estática y sobrevive al cambio de escena. Se vacía acá, que es
        // justo cuando se van a volver a llenar: si no, los pozos del planeta anterior
        // seguirían empujando arañas en sitios donde ya no hay nada.
        Todos.Clear();

        if (itemPositions == null || itemPositions.Count == 0) return;

        int cuantos = Mathf.Max(1, Mathf.RoundToInt(itemPositions.Count * fractionOfItems));
        var elegidas = new List<Vector3>(itemPositions);
        Shuffle(elegidas);

        int levantados = 0;
        var centros = new List<Vector3>();

        for (int i = 0; i < cuantos && i < elegidas.Count; i++)
            if (TryPlace(elegidas[i], walkableCells, spawnPoint, centros)) levantados++;

        Debug.Log($"Pozos: {levantados} de {cuantos} intentados.");
    }

    bool TryPlace(Vector3 itemPosition, IReadOnlyList<Vector3> walkableCells, Vector3 spawnPoint,
                  List<Vector3> yaPuestos)
    {
        for (int intento = 0; intento < 8; intento++)
        {
            float angle = Random.Range(0f, 360f);
            var dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector3 center = itemPosition + dir * Random.Range(distanceFromItem.x, distanceFromItem.y);

            // Nunca en el punto de aterrizaje: ahí abres los ojos y tienes que ver.
            if (Vector3.Distance(center, spawnPoint) < 22f) continue;
            if (DemasiadoCerca(center, yaPuestos)) continue;

            // Suelo bajo TODA la huella, no solo bajo el centro. El terreno son islas con
            // vacío entre ellas, y el brocal mide tres metros de ancho: comprobando solo
            // el centro, uno puesto cerca de un borde queda con media pared colgando en
            // el aire. Es exactamente lo que pasaba.
            if (!SueloBajoLaHuella(center, walkableCells, out float sueloY, out float desnivel)) continue;

            var sitio = new Vector3(center.x, sueloY, center.z);
            Build(sitio, desnivel);
            yaPuestos.Add(sitio);
            return true;
        }

        return false;
    }

    bool DemasiadoCerca(Vector3 center, List<Vector3> yaPuestos)
    {
        float minimo = outerRadius.y * 6f;

        for (int i = 0; i < yaPuestos.Count; i++)
        {
            Vector3 d = yaPuestos[i] - center;
            d.y = 0f;
            if (d.sqrMagnitude < minimo * minimo) return true;
        }

        return false;
    }

    // El pozo se construye ENTERO en coordenadas locales, con el objeto puesto en su
    // sitio y todo lo demás relativo a él.
    //
    // Antes la malla se generaba con coordenadas del mundo y el hijo se corría hacia
    // atrás para compensar. Funciona, pero es exactamente la clase de montaje donde una
    // compensación de más o de menos manda la piedra al doble de distancia del origen sin
    // que nada falle ni avise. En local no hay nada que compensar.
    //
    // 'center.y' ya viene siendo el punto más bajo de la huella, y 'desnivel' es cuánto
    // sube el terreno por el lado contrario: el muro se alarga esa cantidad hacia abajo
    // para que por el lado alto quede medio enterrado en vez de flotando.
    void Build(Vector3 center, float desnivel)
    {
        var pozo = new GameObject("Pozo").transform;
        pozo.SetParent(transform);
        pozo.position = center;

        float rExt = Random.Range(outerRadius.x, outerRadius.y);
        float grosor = Random.Range(wallThickness.x, wallThickness.y);
        float alto = Random.Range(wallHeight.x, wallHeight.y);
        float rMedio = rExt - grosor * 0.5f;
        float altoTotal = alto + desnivel;

        Todos.Add((center, rExt));

        BuildStonework(pozo, rMedio, grosor, altoTotal);
        BuildOccluder(pozo, rMedio, grosor, altoTotal);
        BuildMouth(pozo, rExt - grosor);
        BuildHideSpot(pozo, center, center.y + altoTotal);
    }

    // ── La piedra ───────────────────────────────────────────────────────────────
    //
    // Todo el brocal es UNA malla, no un objeto por piedra. Son más de cien piedras por
    // pozo y varios pozos por mapa: como objetos sueltos serían cientos de dibujados por
    // cuadro para algo que nunca se mueve ni se toca por separado.
    void BuildStonework(Transform pozo, float rMedio, float grosor, float altoTotal)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();

        int hiladas = Random.Range(courses.x, courses.y + 1);
        float hHilada = altoTotal / hiladas;

        for (int c = 0; c < hiladas; c++)
        {
            int piedras = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * rMedio / stoneWidth));
            float paso = Mathf.PI * 2f / piedras;

            // Cada hilada arranca corrida media piedra respecto de la anterior. Es lo que
            // hace que las juntas verticales no se alineen, que es la diferencia entre un
            // muro de piedra y una cuadrícula.
            float desfase = (c % 2) * paso * 0.5f + Random.Range(-paso * 0.15f, paso * 0.15f);

            float yBase = c * hHilada;

            for (int i = 0; i < piedras; i++)
            {
                float a = desfase + i * paso;
                var haciaFuera = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tangente = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));

                // Cada piedra ocupa su hueco menos una junta, y de ahí para abajo según
                // el desorden. Piedras que se pasan del hueco se encajarían entre sí.
                float ancho = paso * rMedio * (1f - Random.Range(0.06f, 0.06f + stoneJitter));
                float altoP = hHilada * (1f - Random.Range(0.05f, 0.05f + stoneJitter * 0.6f));
                float fondo = grosor * (1f - Random.Range(0f, stoneJitter * 0.4f));

                // Unas asoman y otras se hunden: es de donde sale el relieve.
                float relieve = Random.Range(-reliefDepth, reliefDepth);
                Vector3 centroP = haciaFuera * (rMedio + relieve)
                                + Vector3.up * (yBase + hHilada * 0.5f);

                // Inclinación mínima. Las piedras de un muro seco no están perfectamente a
                // nivel, pero tampoco tumbadas: se caería.
                var giro = Quaternion.LookRotation(haciaFuera, Vector3.up)
                         * Quaternion.Euler(Random.Range(-4f, 4f), 0f, Random.Range(-5f, 5f));

                AgregarCaja(verts, tris, centroP, giro, new Vector3(ancho, altoP, fondo));
            }
        }

        // La coronación: losas planas, más anchas que la pared, que sobresalen por fuera.
        // Es lo que remata el brocal y le da el borde grueso donde se apoyan las manos.
        int losas = Random.Range(capStones.x, capStones.y + 1);
        float pasoLosa = Mathf.PI * 2f / losas;
        float desfaseLosa = Random.Range(0f, pasoLosa);

        for (int i = 0; i < losas; i++)
        {
            float a = desfaseLosa + i * pasoLosa;
            var haciaFuera = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

            float altoL = Random.Range(capHeight.x, capHeight.y);
            float ancho = pasoLosa * rMedio * Random.Range(0.86f, 0.99f);
            float fondo = grosor + capOverhang * 2f;

            Vector3 centroL = haciaFuera * rMedio
                            + Vector3.up * (altoTotal - altoL * 0.5f);

            var giro = Quaternion.LookRotation(haciaFuera, Vector3.up)
                     * Quaternion.Euler(Random.Range(-3f, 3f), 0f, Random.Range(-4f, 4f));

            AgregarCaja(verts, tris, centroL, giro, new Vector3(ancho, altoL, fondo));
        }

        var mesh = new Mesh { name = "Brocal" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);

        // Las caras no comparten vértices, así que esto da sombreado facetado, que es lo
        // que corresponde al resto del planeta.
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("Piedra");
        go.transform.SetParent(pozo, false);
        go.transform.localPosition = Vector3.zero;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = stoneMaterial;
    }

    // Caja orientada, con las seis caras sin compartir vértices.
    static void AgregarCaja(List<Vector3> verts, List<int> tris, Vector3 centro,
                            Quaternion giro, Vector3 tamano)
    {
        Vector3 d = giro * Vector3.forward * (tamano.z * 0.5f);   // hacia fuera del muro
        Vector3 w = giro * Vector3.right * (tamano.x * 0.5f);     // a lo largo del muro
        Vector3 h = giro * Vector3.up * (tamano.y * 0.5f);

        // Las ocho esquinas.
        Vector3[] p =
        {
            centro - w - h - d, centro + w - h - d, centro + w + h - d, centro - w + h - d,
            centro - w - h + d, centro + w - h + d, centro + w + h + d, centro - w + h + d,
        };

        int[][] caras =
        {
            new[] { 4, 5, 6, 7 },   // fuera
            new[] { 1, 0, 3, 2 },   // dentro
            new[] { 3, 7, 6, 2 },   // arriba
            new[] { 0, 1, 5, 4 },   // abajo
            new[] { 5, 1, 2, 6 },   // costados
            new[] { 0, 4, 7, 3 },
        };

        foreach (var cara in caras)
        {
            int b = verts.Count;
            foreach (int idx in cara) verts.Add(p[idx]);
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
        }
    }

    // ── Lo que tapa de verdad ───────────────────────────────────────────────────
    //
    // Un anillo de cajas invisibles con collider. Hace dos trabajos a la vez: es la pared
    // sólida con la que choca el jugador, y es lo que corta el rayo de la araña.
    //
    // No se usa la malla de piedra para esto, y es a propósito. Entre piedra y piedra
    // quedan juntas, y por cualquiera de ellas se colaría el rayo: el jugador vería un
    // muro macizo delante y la araña lo oiría igual. Es la peor clase de fallo, el que
    // parece injusto.
    void BuildOccluder(Transform pozo, float rMedio, float grosor, float altoTotal)
    {
        float paso = Mathf.PI * 2f / occluderSegments;

        // Cada segmento se pasa de su arco para solaparse con el vecino. Sin solape
        // quedan rendijas justo en las juntas, que es donde el rayo va a buscar.
        float largo = 2f * rMedio * Mathf.Sin(paso * 0.5f) * 1.4f;

        float alto = altoTotal + occluderSink;
        float centroY = altoTotal - alto * 0.5f;

        for (int i = 0; i < occluderSegments; i++)
        {
            float a = i * paso;
            var haciaFuera = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Pared";
            go.layer = Layer;
            go.transform.SetParent(pozo, false);
            go.transform.localPosition = haciaFuera * rMedio + Vector3.up * centroY;
            go.transform.localRotation = Quaternion.LookRotation(haciaFuera, Vector3.up);
            go.transform.localScale = new Vector3(largo, alto, grosor);

            go.GetComponent<MeshRenderer>().enabled = false;
        }
    }

    // La boca. Un disco oscuro dentro del brocal: sin él se ve el mismo suelo del planeta
    // ahí dentro y el pozo se lee como un anillo apoyado, no como algo con fondo.
    void BuildMouth(Transform pozo, float rInterior)
    {
        var boca = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        boca.name = "Boca";
        boca.transform.SetParent(pozo, false);
        boca.transform.localPosition = Vector3.up * 0.03f;
        boca.transform.localScale = new Vector3(rInterior * 2f, 0.03f, rInterior * 2f);
        Destroy(boca.GetComponent<Collider>());

        if (floorMaterial != null) boca.GetComponent<Renderer>().sharedMaterial = floorMaterial;
    }

    // El puesto va en el centro, a ras de suelo. No hace falta arrimarlo a ninguna pared:
    // el interior mide poco más de dos metros y el brocal tapa por los cuatro costados.
    void BuildHideSpot(Transform pozo, Vector3 center, float cantoY)
    {
        var go = new GameObject("Refugio");
        go.transform.SetParent(pozo, false);
        go.transform.localPosition = Vector3.zero;

        // Se conserva hacia dónde miraba el jugador al entrar. En un cráter con una sola
        // boca tenía sentido girarlo hacia fuera; en un anillo cerrado no hay un "fuera"
        // mejor que otro, y girarlo a un lado al azar sería peor que dejarlo mirando
        // hacia donde venía.
        go.AddComponent<HideSpot>()
          .Configure(center, Vector3.forward, markerMaterial, true, cantoY);
    }

    // Suelo firme bajo el centro y bajo los ocho puntos del borde. Si alguno cae en el
    // vacío, el sitio no sirve y se prueba otro.
    //
    // Devuelve la altura MÁS BAJA de todas: apoyando en la del centro, el brocal flota
    // por el lado que baja. Apoyando en la mínima, por el lado que sube queda medio
    // enterrado, que es el aspecto que tiene un pozo viejo de verdad.
    bool SueloBajoLaHuella(Vector3 center, IReadOnlyList<Vector3> walkableCells,
                           out float groundY, out float desnivel)
    {
        groundY = center.y;
        desnivel = 0f;

        if (!SueloDebajo(center, walkableCells, out float centroY)) return false;

        groundY = centroY;
        float techo = centroY;
        float radio = outerRadius.y;

        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            var p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radio;

            if (!SueloDebajo(p, walkableCells, out float y)) return false;
            groundY = Mathf.Min(groundY, y);
            techo = Mathf.Max(techo, y);
        }

        desnivel = techo - groundY;

        // Las celdas pisables no son la cara del terreno: traen incorporado el margen
        // desde el que se suelta al jugador. Heredarlo dejaría el pozo flotando ese
        // mismo margen sobre el suelo. Es el mismo descuento que hace el módulo de
        // ascenso para no quedarse en el aire.
        groundY -= MargenDeCeldaPisable;
        return true;
    }

    // Lo que el generador de terreno le suma a cada celda pisable como holgura.
    const float MargenDeCeldaPisable = 0.3f;

    bool SueloDebajo(Vector3 point, IReadOnlyList<Vector3> walkableCells, out float groundY)
    {
        groundY = point.y;
        if (walkableCells == null) return false;

        float mejor = float.MaxValue;
        bool hay = false;

        for (int i = 0; i < walkableCells.Count; i++)
        {
            Vector3 delta = walkableCells[i] - point;
            delta.y = 0f;

            float d = delta.sqrMagnitude;
            if (d > groundTolerance * groundTolerance || d >= mejor) continue;

            mejor = d;
            groundY = walkableCells[i].y;
            hay = true;
        }

        return hay;
    }

    static void Shuffle(List<Vector3> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
