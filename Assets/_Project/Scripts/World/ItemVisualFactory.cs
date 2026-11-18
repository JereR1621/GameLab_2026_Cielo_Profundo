using UnityEngine;

// Construye la forma de cada objeto con primitivas simples, igual que la silueta
// del personaje. Nada de esferas genéricas: cada ficha del catálogo es un
// instrumento concreto que la civilización anterior dejó atrás.
public static class ItemVisualFactory
{
    // Instancia un objeto recogible completo. Sin collider: la detección la hace
    // PlayerInteraction por distancia, y un collider acá solo estorbaría al caminar.
    public static Collectible Spawn(CatalogEntry entry, Vector3 position, Transform parent,
                                    Material body, Material accent,
                                    float lightRange = 2.4f, float lightIntensity = 1.3f)
    {
        var go = new GameObject("Item_" + entry.name);
        go.transform.SetParent(parent);
        go.transform.position = position;

        var collectible = go.AddComponent<Collectible>();
        collectible.SetEntry(entry);
        collectible.SetVisual(Build(entry, body, accent).transform);
        AddGlow(go.transform, entry, lightRange, lightIntensity);
        return collectible;
    }

    // Luz mínima, solo para despegar la ficha del suelo cuando ya estás encima. El
    // rango corto es la parte importante: si llegara lejos, encontrar objetos dejaría
    // de depender del radar y bastaría con mirar el horizonte.
    //
    // Va como hija del objeto, así que acompaña el vaivén, y se destruye con él al
    // recogerlo sin tener que limpiarla aparte.
    static void AddGlow(Transform parent, CatalogEntry entry, float range, float intensity)
    {
        var glowGO = new GameObject("Resplandor");
        glowGO.transform.SetParent(parent, false);
        glowGO.transform.localPosition = new Vector3(0f, 0.18f, 0f);

        var glow = glowGO.AddComponent<Light>();
        glow.type = LightType.Point;
        // Se lava hacia el blanco: con el color de acento puro, las fichas más
        // saturadas teñían el terreno de alrededor y parecían otra cosa.
        glow.color = Color.Lerp(entry.accentColor, Color.white, 0.35f);
        glow.range = range;
        glow.intensity = intensity;
        glow.shadows = LightShadows.None;
    }

    public static GameObject Build(CatalogEntry entry, Material body, Material accent)
    {
        var root = new GameObject("ItemVisual");

        switch (entry.shape)
        {
            case ItemShape.Lampara: BuildLampara(root.transform, body, accent); break;
            case ItemShape.Lente: BuildLente(root.transform, body, accent); break;
            case ItemShape.Antena: BuildAntena(root.transform, body, accent); break;
            case ItemShape.Tablilla: BuildTablilla(root.transform, body, accent); break;
            case ItemShape.Frasco: BuildFrasco(root.transform, body, accent); break;
            case ItemShape.Carrete: BuildCarrete(root.transform, body, accent); break;
            case ItemShape.Giroscopio: BuildGiroscopio(root.transform, body, accent); break;
            case ItemShape.Baliza: BuildBaliza(root.transform, body, accent); break;
            case ItemShape.Casco: BuildCasco(root.transform, body, accent); break;
            case ItemShape.Credencial: BuildCredencial(root.transform, body, accent); break;
        }

        return root;
    }

    static void BuildFrasco(Transform parent, Material body, Material accent)
    {
        Add(parent, PrimitiveType.Cylinder, "Cuerpo", new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(0.17f, 0.14f, 0.17f), body);
        Add(parent, PrimitiveType.Cylinder, "Contenido", new Vector3(0f, 0.13f, 0f), Vector3.zero, new Vector3(0.14f, 0.11f, 0.14f), accent);
        Add(parent, PrimitiveType.Cylinder, "Cuello", new Vector3(0f, 0.33f, 0f), Vector3.zero, new Vector3(0.07f, 0.06f, 0.07f), body);
        Add(parent, PrimitiveType.Cylinder, "Tapa", new Vector3(0f, 0.41f, 0f), Vector3.zero, new Vector3(0.10f, 0.03f, 0.10f), body);
        Add(parent, PrimitiveType.Cube, "Etiqueta", new Vector3(0f, 0.16f, 0.16f), Vector3.zero, new Vector3(0.12f, 0.09f, 0.01f), body);
    }

    static void BuildCarrete(Transform parent, Material body, Material accent)
    {
        Add(parent, PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.22f, 0.02f, 0.22f), body);
        Add(parent, PrimitiveType.Cylinder, "Eje", new Vector3(0f, 0.20f, 0f), Vector3.zero, new Vector3(0.05f, 0.18f, 0.05f), body);
        Add(parent, PrimitiveType.Cylinder, "BridaBaja", new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(0.32f, 0.015f, 0.32f), body);
        Add(parent, PrimitiveType.Cylinder, "BridaAlta", new Vector3(0f, 0.34f, 0f), Vector3.zero, new Vector3(0.32f, 0.015f, 0.32f), body);
        Add(parent, PrimitiveType.Cylinder, "Cinta", new Vector3(0f, 0.245f, 0f), Vector3.zero, new Vector3(0.26f, 0.08f, 0.26f), accent);
    }

    static void BuildGiroscopio(Transform parent, Material body, Material accent)
    {
        Add(parent, PrimitiveType.Cylinder, "Pie", new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.20f, 0.02f, 0.20f), body);
        Add(parent, PrimitiveType.Cube, "Soporte", new Vector3(0f, 0.11f, 0f), Vector3.zero, new Vector3(0.04f, 0.18f, 0.04f), body);

        // Los aros son marcos de cuatro barras: no hay primitiva con agujero, y un
        // disco macizo en tres orientaciones se vería como una bola, no como un gimbal.
        AddSquareRing(parent, "AroExterior", new Vector3(0f, 0.33f, 0f), Vector3.zero, 0.22f, 0.022f, body);
        AddSquareRing(parent, "AroInterior", new Vector3(0f, 0.33f, 0f), new Vector3(0f, 90f, 0f), 0.17f, 0.020f, body);
        Add(parent, PrimitiveType.Sphere, "Nucleo", new Vector3(0f, 0.33f, 0f), Vector3.zero, new Vector3(0.12f, 0.12f, 0.12f), accent);
    }

    static void BuildBaliza(Transform parent, Material body, Material accent)
    {
        // El estrechamiento se finge apilando cilindros de radio decreciente: la
        // primitiva de Unity no tiene forma cónica.
        Add(parent, PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.03f, 0f), Vector3.zero, new Vector3(0.24f, 0.03f, 0.24f), body);
        Add(parent, PrimitiveType.Cylinder, "CuerpoBajo", new Vector3(0f, 0.14f, 0f), Vector3.zero, new Vector3(0.19f, 0.09f, 0.19f), body);
        Add(parent, PrimitiveType.Cylinder, "CuerpoAlto", new Vector3(0f, 0.30f, 0f), Vector3.zero, new Vector3(0.13f, 0.08f, 0.13f), body);
        Add(parent, PrimitiveType.Sphere, "Domo", new Vector3(0f, 0.44f, 0f), Vector3.zero, new Vector3(0.17f, 0.14f, 0.17f), accent);

        for (int i = 0; i < 3; i++)
        {
            var bar = Add(parent, PrimitiveType.Cube, "Reja" + i, Vector3.zero, Vector3.zero, new Vector3(0.02f, 0.17f, 0.02f), body);
            var rot = Quaternion.Euler(0f, i * 120f, 0f);
            bar.transform.localPosition = rot * new Vector3(0.11f, 0.44f, 0f);
        }
    }

    static void BuildCasco(Transform parent, Material body, Material accent)
    {
        Add(parent, PrimitiveType.Cylinder, "AnilloCuello", new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(0.20f, 0.05f, 0.20f), body);

        var shell = AddMesh(parent, "Cupula", LowPolyMeshFactory.CreateFacetedSphere(10, 7, 0.21f), body);
        shell.transform.localPosition = new Vector3(0f, 0.24f, 0f);
        shell.transform.localScale = new Vector3(1f, 0.97f, 1.02f);

        var glass = AddMesh(parent, "Visor", LowPolyMeshFactory.CreateFacetedSphere(10, 5, 0.19f), accent);
        glass.transform.localPosition = new Vector3(0f, 0.245f, 0.05f);
        glass.transform.localScale = new Vector3(0.92f, 0.60f, 0.95f);
    }

    static void BuildCredencial(Transform parent, Material body, Material accent)
    {
        var plate = Add(parent, PrimitiveType.Cube, "Placa", new Vector3(0f, 0.24f, 0f), new Vector3(-8f, 0f, 0f), new Vector3(0.26f, 0.34f, 0.02f), body);
        Add(plate.transform, PrimitiveType.Cube, "Foto", new Vector3(0f, 0.20f, -0.6f), Vector3.zero, new Vector3(0.5f, 0.34f, 0.3f), accent);
        Add(plate.transform, PrimitiveType.Cube, "Linea1", new Vector3(0f, -0.10f, -0.6f), Vector3.zero, new Vector3(0.66f, 0.07f, 0.3f), accent);
        Add(plate.transform, PrimitiveType.Cube, "Linea2", new Vector3(-0.1f, -0.26f, -0.6f), Vector3.zero, new Vector3(0.44f, 0.06f, 0.3f), accent);
        Add(parent, PrimitiveType.Cube, "Pinza", new Vector3(0f, 0.43f, 0f), Vector3.zero, new Vector3(0.09f, 0.05f, 0.04f), body);
        Add(parent, PrimitiveType.Cube, "Pie", new Vector3(0f, 0.03f, 0f), Vector3.zero, new Vector3(0.18f, 0.05f, 0.10f), body);
    }

    static void AddSquareRing(Transform parent, string name, Vector3 center, Vector3 euler, float radius, float thickness, Material mat)
    {
        var ring = new GameObject(name);
        ring.transform.SetParent(parent, false);
        ring.transform.localPosition = center;
        ring.transform.localRotation = Quaternion.Euler(euler);

        float span = radius * 2f;
        Add(ring.transform, PrimitiveType.Cube, "Arriba", new Vector3(0f, radius, 0f), Vector3.zero, new Vector3(span, thickness, thickness), mat);
        Add(ring.transform, PrimitiveType.Cube, "Abajo", new Vector3(0f, -radius, 0f), Vector3.zero, new Vector3(span, thickness, thickness), mat);
        Add(ring.transform, PrimitiveType.Cube, "Izq", new Vector3(-radius, 0f, 0f), Vector3.zero, new Vector3(thickness, span, thickness), mat);
        Add(ring.transform, PrimitiveType.Cube, "Der", new Vector3(radius, 0f, 0f), Vector3.zero, new Vector3(thickness, span, thickness), mat);
    }

    static GameObject AddMesh(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static void BuildLampara(Transform parent, Material body, Material accent)
    {
        Add(parent, PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.22f, 0.02f, 0.22f), body);
        Add(parent, PrimitiveType.Cylinder, "Poste", new Vector3(0f, 0.16f, 0f), Vector3.zero, new Vector3(0.05f, 0.14f, 0.05f), body);
        Add(parent, PrimitiveType.Cube, "Caja", new Vector3(0f, 0.38f, 0f), Vector3.zero, new Vector3(0.19f, 0.2f, 0.19f), body);
        Add(parent, PrimitiveType.Cube, "Vidrio", new Vector3(0f, 0.38f, 0f), Vector3.zero, new Vector3(0.14f, 0.15f, 0.21f), accent);
        Add(parent, PrimitiveType.Cube, "Tapa", new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(0.23f, 0.04f, 0.23f), body);
        Add(parent, PrimitiveType.Cube, "Asa", new Vector3(0f, 0.58f, 0f), Vector3.zero, new Vector3(0.03f, 0.12f, 0.03f), body);
    }

    static void BuildLente(Transform parent, Material body, Material accent)
    {
        Add(parent, PrimitiveType.Cylinder, "Aro", new Vector3(0f, 0.4f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.34f, 0.02f, 0.34f), body);
        Add(parent, PrimitiveType.Cylinder, "Cristal", new Vector3(0f, 0.4f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.27f, 0.03f, 0.27f), accent);
        Add(parent, PrimitiveType.Cube, "Mango", new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(0.05f, 0.24f, 0.05f), body);
        Add(parent, PrimitiveType.Cube, "Cuña", new Vector3(0f, 0.25f, 0f), new Vector3(0f, 0f, 35f), new Vector3(0.14f, 0.03f, 0.05f), body);
    }

    static void BuildAntena(Transform parent, Material body, Material accent)
    {
        Add(parent, PrimitiveType.Cylinder, "Pie", new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.18f, 0.02f, 0.18f), body);
        Add(parent, PrimitiveType.Cylinder, "Mástil", new Vector3(0f, 0.26f, 0f), Vector3.zero, new Vector3(0.04f, 0.24f, 0.04f), body);

        for (int i = 0; i < 3; i++)
        {
            float yaw = i * 120f;
            var arm = Add(parent, PrimitiveType.Cube, "Brazo" + i, Vector3.zero, Vector3.zero, new Vector3(0.03f, 0.22f, 0.03f), body);
            arm.transform.localRotation = Quaternion.Euler(0f, yaw, 32f);
            arm.transform.localPosition = arm.transform.localRotation * new Vector3(0f, 0.12f, 0f) + new Vector3(0f, 0.5f, 0f);
        }

        Add(parent, PrimitiveType.Sphere, "Receptor", new Vector3(0f, 0.52f, 0f), Vector3.zero, new Vector3(0.1f, 0.1f, 0.1f), accent);
    }

    static void BuildTablilla(Transform parent, Material body, Material accent)
    {
        var slab = Add(parent, PrimitiveType.Cube, "Losa", new Vector3(0f, 0.26f, 0f), new Vector3(-12f, 0f, 0f), new Vector3(0.34f, 0.46f, 0.05f), body);
        Add(slab.transform, PrimitiveType.Cube, "Grabado", new Vector3(0f, 0.12f, -0.6f), Vector3.zero, new Vector3(0.6f, 0.1f, 0.2f), accent);
        Add(slab.transform, PrimitiveType.Cube, "Grabado2", new Vector3(0f, -0.08f, -0.6f), Vector3.zero, new Vector3(0.42f, 0.07f, 0.2f), accent);
        Add(parent, PrimitiveType.Cube, "Zócalo", new Vector3(0f, 0.03f, 0f), Vector3.zero, new Vector3(0.26f, 0.06f, 0.14f), body);
    }

    static GameObject Add(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localEuler, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        SafeDestroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // Esta fábrica corre tanto en runtime como desde los builders de escena en el
    // editor. Destroy no surte efecto inmediato fuera de Play, así que hay que elegir.
    static void SafeDestroy(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }
}
