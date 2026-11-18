using UnityEngine;

public enum StructureKind
{
    Refugio,
    CasaQuemada,
    Ruina,
    Torre,
}

// Construye las estructuras con cajas, igual que la silueta del personaje.
// Todas las que guardan un objeto tienen un vano de entrada de 1.6 de ancho:
// el jugador mide 0.8 de diámetro, así que entra sin pelearse con la pared.
public static class StructureFactory
{
    public const float WallHeight = 2.6f;
    const float HalfSize = 2.2f;
    const float Thickness = 0.25f;

    public static GameObject Build(StructureKind kind, Material wall, Material charred, Material ember, int seed)
    {
        var root = new GameObject("Estructura_" + kind);
        var rng = new System.Random(seed);

        switch (kind)
        {
            case StructureKind.Refugio:
                BuildShell(root.transform, wall, roof: true, brokenWalls: false, rng);
                break;

            case StructureKind.CasaQuemada:
                BuildShell(root.transform, charred, roof: false, brokenWalls: true, rng);
                AddFire(root.transform, charred, ember, rng);
                break;

            case StructureKind.Ruina:
                BuildShell(root.transform, wall, roof: false, brokenWalls: true, rng);
                break;

            case StructureKind.Torre:
                BuildTower(root.transform, wall, ember);
                break;
        }

        return root;
    }

    public static bool HoldsItem(StructureKind kind) => kind != StructureKind.Torre;

    static void BuildShell(Transform parent, Material mat, bool roof, bool brokenWalls, System.Random rng)
    {
        float h = brokenWalls ? WallHeight * 0.62f : WallHeight;
        float half = HalfSize;

        // Pared del fondo. Si está en ruinas, se parte en dos trozos desparejos.
        if (brokenWalls)
        {
            AddBox(parent, "MuroFondoA", new Vector3(-1.2f, h * 0.5f, half), new Vector3(2f, h, Thickness), mat);
            AddBox(parent, "MuroFondoB", new Vector3(1.5f, h * 0.34f, half), new Vector3(1.4f, h * 0.68f, Thickness), mat);
        }
        else
        {
            AddBox(parent, "MuroFondo", new Vector3(0f, h * 0.5f, half), new Vector3(half * 2f, h, Thickness), mat);
        }

        // Frente con el vano de entrada.
        AddBox(parent, "MuroFrenteA", new Vector3(-1.5f, h * 0.5f, -half), new Vector3(1.4f, h, Thickness), mat);
        AddBox(parent, "MuroFrenteB", new Vector3(1.5f, h * 0.5f, -half), new Vector3(1.4f, h, Thickness), mat);
        if (!brokenWalls)
            AddBox(parent, "Dintel", new Vector3(0f, h - 0.25f, -half), new Vector3(1.6f, 0.5f, Thickness), mat);

        AddBox(parent, "MuroEste", new Vector3(half, h * 0.5f, 0f), new Vector3(Thickness, h, half * 2f), mat);

        if (brokenWalls)
            AddBox(parent, "MuroOeste", new Vector3(-half, h * 0.4f, -0.8f), new Vector3(Thickness, h * 0.8f, 2.6f), mat);
        else
            AddBox(parent, "MuroOeste", new Vector3(-half, h * 0.5f, 0f), new Vector3(Thickness, h, half * 2f), mat);

        if (roof)
        {
            AddBox(parent, "TechoA", new Vector3(0f, WallHeight + 0.55f, -1.15f), new Vector3(4.9f, 0.2f, 2.9f), mat,
                   new Vector3(-26f, 0f, 0f));
            AddBox(parent, "TechoB", new Vector3(0f, WallHeight + 0.55f, 1.15f), new Vector3(4.9f, 0.2f, 2.9f), mat,
                   new Vector3(26f, 0f, 0f));
        }
        else if (rng.Next(2) == 0)
        {
            // Una sola viga caída: se lee como techo que se vino abajo.
            AddBox(parent, "VigaCaida", new Vector3(0.4f, h * 0.55f, 0.3f), new Vector3(4.2f, 0.18f, 0.5f), mat,
                   new Vector3(0f, 22f, -34f));
        }
    }

    static void BuildTower(Transform parent, Material mat, Material ember)
    {
        const float legOffset = 0.9f;
        const float height = 6.5f;

        for (int i = 0; i < 4; i++)
        {
            float sx = (i % 2 == 0) ? -legOffset : legOffset;
            float sz = (i < 2) ? -legOffset : legOffset;
            AddBox(parent, "Pata" + i, new Vector3(sx, height * 0.5f, sz), new Vector3(0.22f, height, 0.22f), mat);
        }

        AddBox(parent, "Plataforma", new Vector3(0f, height, 0f), new Vector3(2.6f, 0.25f, 2.6f), mat);
        AddBox(parent, "Baranda", new Vector3(0f, height + 0.5f, 1.25f), new Vector3(2.6f, 0.75f, 0.12f), mat);
        AddBox(parent, "Mastil", new Vector3(0f, height + 1.4f, 0f), new Vector3(0.14f, 2.4f, 0.14f), mat);
        AddBox(parent, "Baliza", new Vector3(0f, height + 2.6f, 0f), new Vector3(0.3f, 0.3f, 0.3f), ember);
    }

    // Fuego sin sistema de partículas: lenguas facetadas que se animan a 12 fps,
    // igual que el personaje. Es lo que hacían los juegos de la época y encaja
    // mucho mejor con el resto que unas partículas suaves y modernas.
    static void AddFire(Transform parent, Material charred, Material ember, System.Random rng)
    {
        var fireRoot = new GameObject("Fuego");
        fireRoot.transform.SetParent(parent, false);
        fireRoot.transform.localPosition = new Vector3(0.3f, 0.1f, 0.4f);

        AddBox(fireRoot.transform, "Escombro", new Vector3(0f, 0.15f, 0f), new Vector3(1.6f, 0.3f, 1.4f), charred);

        var tongues = new Transform[4];
        for (int i = 0; i < tongues.Length; i++)
        {
            var go = new GameObject("Llama" + i);
            go.transform.SetParent(fireRoot.transform, false);
            go.transform.localPosition = new Vector3(
                (float)(rng.NextDouble() - 0.5) * 1.1f, 0.55f, (float)(rng.NextDouble() - 0.5) * 1.0f);

            go.AddComponent<MeshFilter>().sharedMesh =
                LowPolyMeshFactory.CreateCrystal(5, 1.5f, 0.28f, rng.Next(99999));
            go.AddComponent<MeshRenderer>().sharedMaterial = ember;
            tongues[i] = go.transform;
        }

        var lightGO = new GameObject("LuzFuego");
        lightGO.transform.SetParent(fireRoot.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        var glow = lightGO.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.55f, 0.28f);
        glow.range = 12f;
        glow.intensity = 3.2f;

        var flicker = fireRoot.AddComponent<FlameFlicker>();
        flicker.Configure(tongues, glow);
    }

    static GameObject AddBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat,
                             Vector3 localEuler = default)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPos;
        box.transform.localRotation = Quaternion.Euler(localEuler);
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = mat;
        return box;
    }
}
