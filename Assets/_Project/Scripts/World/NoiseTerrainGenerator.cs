using System;
using System.Collections.Generic;
using UnityEngine;

public class NoiseTerrainGenerator : MonoBehaviour
{
    [SerializeField] PlanetTheme theme;
    [SerializeField] Transform playerToPlace;
    [SerializeField] float spawnClearance = 1.5f;

    [Header("Testing")]
    [Tooltip("Sortea una semilla nueva en cada Play, sin tocar el asset del tema. Desactivar para reproducir siempre la misma semilla fija (el comportamiento real cuando un planeta ya está identificado).")]
    [SerializeField] bool randomizeSeedOnPlay = true;

    public Vector3 GroundSpawnPoint { get; private set; }
    public int EffectiveSeed { get; private set; }

    int seedOverride;
    bool hasSeedOverride;

    Color colorOverride;
    bool hasColorOverride;

    // Las fija PlanetSession en su Awake, antes de que este Start genere el terreno.
    // Es lo que hace que un cuerpo de la ruta sea siempre el mismo lugar.
    public void OverrideSeed(int seed)
    {
        seedOverride = seed;
        hasSeedOverride = true;
    }

    public void OverrideBaseColor(Color color)
    {
        colorOverride = color;
        hasColorOverride = true;
    }
    public event Action<Vector3> OnTerrainReady;

    readonly List<Vector3> walkableCellPositions = new List<Vector3>();
    public IReadOnlyList<Vector3> WalkableCellPositions => walkableCellPositions;

    // Pools de mallas compartidas: con miles de celdas, una malla por celda sería
    // insostenible. Unas pocas variantes rotadas al azar ya rompen la repetición.
    const int ChunkVariants = 10;
    Mesh[] rockMeshes;
    Mesh[] dirtMeshes;

    Material rockMaterial;
    Material dirtMaterial;

    // Umbral del campo de materiales: lo decide la semilla, así hay planetas
    // casi todos de piedra y otros casi todos de tierra.
    float rockBias;
    float materialFieldOffset;
    float hillOffset;

    void Start()
    {
        if (theme == null)
        {
            Debug.LogWarning("NoiseTerrainGenerator sin PlanetTheme asignado.");
            return;
        }
        Generate();
    }

    public void Generate()
    {
        int effectiveSeed = hasSeedOverride
            ? seedOverride
            : (randomizeSeedOnPlay ? UnityEngine.Random.Range(0, 999999) : theme.seed);
        EffectiveSeed = effectiveSeed;
        Debug.Log("Generando planeta con semilla " + effectiveSeed);

        walkableCellPositions.Clear();
        BuildRockPool(effectiveSeed);

        int n = theme.gridSize;
        bool[,] solid = new bool[n, n];
        float offsetX = effectiveSeed * 13.37f;
        float offsetY = effectiveSeed * 71.11f;

        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
            {
                float nx = (x + offsetX) * theme.noiseScale;
                float nz = (z + offsetY) * theme.noiseScale;
                solid[x, z] = Mathf.PerlinNoise(nx, nz) > theme.solidThreshold;
            }

        // Zona de aterrizaje garantizada: sólida siempre, sin importar qué diga el ruido ahí.
        // Evita que una semilla mala te deje cayendo sobre una isla de 2x2.
        int cx = n / 2, cz = n / 2, r = theme.spawnPatchRadius;
        for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(n - 1, cx + r); x++)
            for (int z = Mathf.Max(0, cz - r); z <= Mathf.Min(n - 1, cz + r); z++)
                solid[x, z] = true;

        List<List<Vector2Int>> components = FindComponents(solid, n);
        if (components.Count == 0)
        {
            Debug.LogWarning("El planeta salió sin tierra sólida. Ajustar solidThreshold o cambiar la semilla.");
            return;
        }

        bool[] isGroundByComponent = new bool[components.Count];
        float[] componentHeight = new float[components.Count];
        for (int i = 0; i < components.Count; i++)
        {
            isGroundByComponent[i] = components[i].Count >= theme.groundMinCells;
            componentHeight[i] = isGroundByComponent[i] ? 0f : SampleHeight(components[i], offsetX, offsetY);
        }

        // La celda central siempre está forzada sólida (es el parche de aterrizaje garantizado),
        // así que ubicamos directamente el componente que la contiene: no hay que adivinar.
        Vector2Int center = new Vector2Int(n / 2, n / 2);
        int rootIndex = FindComponentContaining(components, center);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = theme.fogColor;
        RenderSettings.fogDensity = theme.fogDensity;

        BuildSurfaceMaterials(effectiveSeed);

        // Conecta cada isla con la más cercana ya conectada. Cuando el hueco entre
        // ambas supera el alcance de un salto, rellena el camino con plataformas
        // intermedias en vez de descartar la isla: así no queda nada inaccesible.
        ConnectWithBridges(components, componentHeight, rootIndex);

        for (int i = 0; i < components.Count; i++)
            BuildComponent(components[i], componentHeight[i], isGroundByComponent[i]);

        // Usamos la celda central garantizada como punto de spawn, no un centroide promediado:
        // en una isla de forma irregular el promedio puede caer fuera de la forma real.
        // La altura tiene que incluir el cerro de esa celda, si no aparecemos enterrados.
        Vector3 rootWorld = CellToWorld(center);
        GroundSpawnPoint = rootWorld + Vector3.up * (CellHeight(center, componentHeight[rootIndex]) + spawnClearance);

        PlaceOrRespawnPlayer();
        OnTerrainReady?.Invoke(GroundSpawnPoint);
    }

    void PlaceOrRespawnPlayer()
    {
        if (playerToPlace == null) return;

        var cc = playerToPlace.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        playerToPlace.position = GroundSpawnPoint;
        if (cc != null) cc.enabled = true;

        var respawn = playerToPlace.GetComponent<RespawnOnFall>();
        if (respawn != null) respawn.SetSpawnPoint(GroundSpawnPoint);
    }

    float SampleHeight(List<Vector2Int> cells, float offsetX, float offsetY)
    {
        Vector2Int c = ComponentCentroid(cells);
        float n = Mathf.PerlinNoise((c.x + offsetX) * theme.heightNoiseScale, (c.y + offsetY) * theme.heightNoiseScale);
        return n * theme.platformHeightRange;
    }

    void BuildRockPool(int seed)
    {
        rockMeshes = new Mesh[ChunkVariants];
        dirtMeshes = new Mesh[ChunkVariants];
        for (int i = 0; i < ChunkVariants; i++)
        {
            rockMeshes[i] = LowPolyMeshFactory.CreateGroundChunk(seed * 31 + i, rocky: true);
            dirtMeshes[i] = LowPolyMeshFactory.CreateGroundChunk(seed * 57 + i, rocky: false);
        }

        var rng = new System.Random(seed);
        rockBias = 0.38f + (float)rng.NextDouble() * 0.28f;
        materialFieldOffset = (float)rng.NextDouble() * 500f;
        hillOffset = (float)rng.NextDouble() * 500f;
    }

    void BuildSurfaceMaterials(int seed)
    {
        var shader = Shader.Find("Cielo Profundo/PS1 Vertex Snap");
        Color baseColor = hasColorOverride ? colorOverride : theme.baseColor;

        rockMaterial = new Material(shader);
        rockMaterial.SetColor("_BaseColor", Color.Lerp(baseColor, new Color(0.30f, 0.33f, 0.42f), 0.32f));
        rockMaterial.SetTexture("_MainTex", GenerateSurfaceTexture(seed + 11, harsh: true));
        rockMaterial.SetFloat("_TexScale", 0.5f);
        rockMaterial.SetFloat("_SnapResolution", 140f);

        dirtMaterial = new Material(shader);
        dirtMaterial.SetColor("_BaseColor", Color.Lerp(baseColor, new Color(0.46f, 0.35f, 0.25f), 0.55f));
        dirtMaterial.SetTexture("_MainTex", GenerateSurfaceTexture(seed + 29, harsh: false));
        dirtMaterial.SetFloat("_TexScale", 0.32f);
        dirtMaterial.SetFloat("_SnapResolution", 140f);
    }

    // El tipo de suelo viene de un segundo campo de ruido, no de un sorteo por celda:
    // así salen manchones de tierra y de piedra en vez de un salpicado sin sentido.
    bool IsRockyCell(Vector2Int cell)
    {
        float v = Mathf.PerlinNoise(
            (cell.x + materialFieldOffset) * 0.07f,
            (cell.y + materialFieldOffset) * 0.07f);
        return v > rockBias;
    }

    // Cada celda es un trozo de suelo: malla facetada del pool, rotada en múltiplos
    // de 90° para no repetir el mismo perfil. La colisión sigue siendo una caja simple
    // (un MeshCollider por celda sería carísimo con miles de piedras).
    void BuildGroundCell(string name, Vector3 position, float footprint, float thickness, bool rocky, int variantSeed)
    {
        int variant = Mathf.Abs(variantSeed) % ChunkVariants;

        var go = new GameObject(name);
        go.transform.SetParent(transform);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, 90f * (Mathf.Abs(variantSeed) % 4), 0f);
        go.transform.localScale = new Vector3(footprint, thickness, footprint);

        go.AddComponent<MeshFilter>().sharedMesh = rocky ? rockMeshes[variant] : dirtMeshes[variant];
        go.AddComponent<MeshRenderer>().sharedMaterial = rocky ? rockMaterial : dirtMaterial;
        go.AddComponent<BoxCollider>();
    }

    // Relieve: cada celda se levanta según un tercer campo de ruido, sobre la altura
    // base de su isla. Es lo que convierte una plataforma plana en un terreno con cerros.
    float CellHeight(Vector2Int cell, float baseHeight)
    {
        float n = Mathf.PerlinNoise(
            (cell.x + hillOffset) * theme.hillNoiseScale,
            (cell.y + hillOffset) * theme.hillNoiseScale);
        return baseHeight + n * theme.hillHeight;
    }

    // Altura del terreno en una esquina de la grilla. Como las celdas vecinas
    // comparten esquina, muestrear acá (y no en el centro de cada celda) es lo que
    // hace que la superficie salga continua en vez de un mosaico de piedras sueltas.
    float CornerHeight(int cornerX, int cornerZ, float baseHeight)
    {
        float x = cornerX - 0.5f;
        float z = cornerZ - 0.5f;
        float n = Mathf.PerlinNoise((x + hillOffset) * theme.hillNoiseScale,
                                    (z + hillOffset) * theme.hillNoiseScale);
        return baseHeight + n * theme.hillHeight;
    }

    Vector3 CornerWorld(int cornerX, int cornerZ, float baseHeight)
    {
        float half = theme.gridSize * theme.cellSize * 0.5f;

        // El desvío depende únicamente de las coordenadas de la esquina, nunca de qué
        // celda la está pidiendo. Por eso las celdas vecinas reciben el mismo valor y
        // la malla sigue siendo continua, pero el contorno deja de ser cuadriculado.
        float jx = Mathf.PerlinNoise((cornerX + 31.7f) * 0.87f, (cornerZ + 11.3f) * 0.87f) - 0.5f;
        float jz = Mathf.PerlinNoise((cornerX + 71.1f) * 0.87f, (cornerZ + 53.9f) * 0.87f) - 0.5f;
        float amount = 2f * theme.cornerJitter * theme.cellSize;

        return new Vector3(
            (cornerX - 0.5f) * theme.cellSize - half + jx * amount,
            CornerHeight(cornerX, cornerZ, baseHeight),
            (cornerZ - 0.5f) * theme.cellSize - half + jz * amount);
    }

    static readonly Vector2Int[] CornerOffsets =
    {
        new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1),
    };

    // Vecino que corresponde a cada arista del borde, en el mismo orden cíclico.
    static readonly Vector2Int[] EdgeNeighbors =
    {
        new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0),
    };

    // Una sola malla por isla: tapa continua, paredes solo en el borde y fondo plano.
    // Los triángulos van sin compartir vértices para que el sombreado siga siendo
    // facetado, pero las posiciones salen de la misma función de esquina, así que
    // geométricamente no hay ni juntas ni huecos.
    void BuildComponent(List<Vector2Int> cells, float baseHeight, bool isGround)
    {
        var cellSet = new HashSet<Vector2Int>(cells);

        var verts = new List<Vector3>();
        var rockTris = new List<int>();
        var dirtTris = new List<int>();

        void AddTri(List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        var top = new Vector3[4];
        var bottom = new Vector3[4];

        foreach (var cell in cells)
        {
            for (int i = 0; i < 4; i++)
            {
                top[i] = CornerWorld(cell.x + CornerOffsets[i].x, cell.y + CornerOffsets[i].y, baseHeight);
                bottom[i] = top[i] - Vector3.up * theme.islandThickness;
            }

            var tris = IsRockyCell(cell) ? rockTris : dirtTris;

            AddTri(tris, top[0], top[3], top[2]);
            AddTri(tris, top[0], top[2], top[1]);

            AddTri(tris, bottom[0], bottom[2], bottom[3]);
            AddTri(tris, bottom[0], bottom[1], bottom[2]);

            // Pared solo donde no hay celda vecina: es lo que define el contorno.
            for (int e = 0; e < 4; e++)
            {
                if (cellSet.Contains(cell + EdgeNeighbors[e])) continue;

                Vector3 a = top[e];
                Vector3 b = top[(e + 1) % 4];
                AddTri(tris, a, b, b - Vector3.up * theme.islandThickness);
                AddTri(tris, a, b - Vector3.up * theme.islandThickness, a - Vector3.up * theme.islandThickness);
            }

            walkableCellPositions.Add(CellToWorld(cell) + Vector3.up * (CellHeight(cell, baseHeight) + 0.3f));
        }

        var mesh = new Mesh
        {
            name = isGround ? "Isla" : "Plataforma",
            // Una isla grande pasa cómodamente los 65k vértices del formato de 16 bits.
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            subMeshCount = 2,
        };
        mesh.SetVertices(verts);
        mesh.SetTriangles(rockTris, 0);
        mesh.SetTriangles(dirtTris, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(isGround ? "Isla" : "Plataforma");
        go.transform.SetParent(transform);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { rockMaterial, dirtMaterial };

        // Un MeshCollider por isla en vez de miles de cajas: además de ser lo correcto
        // para una superficie continua, es bastante más liviano.
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    // Ruido de valor con celosía que envuelve: la textura se proyecta desde el mundo,
    // así que si no fuera repetible se verían las costuras como una grilla.
    static Texture2D GenerateSurfaceTexture(int seed, bool harsh)
    {
        const int size = 64;
        const int lattice = 8;

        var rng = new System.Random(seed);
        var coarse = new float[lattice, lattice];
        var fine = new float[lattice * 2, lattice * 2];

        for (int y = 0; y < lattice; y++)
            for (int x = 0; x < lattice; x++)
                coarse[x, y] = (float)rng.NextDouble();

        for (int y = 0; y < lattice * 2; y++)
            for (int x = 0; x < lattice * 2; x++)
                fine[x, y] = (float)rng.NextDouble();

        float Sample(float[,] grid, int cells, float u, float v)
        {
            float fx = u * cells, fy = v * cells;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);

            int xa = ((x0 % cells) + cells) % cells;
            int ya = ((y0 % cells) + cells) % cells;
            int xb = (xa + 1) % cells;
            int yb = (ya + 1) % cells;

            float top = Mathf.Lerp(grid[xa, ya], grid[xb, ya], tx);
            float bottom = Mathf.Lerp(grid[xa, yb], grid[xb, yb], tx);
            return Mathf.Lerp(top, bottom, ty);
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };

        float low = harsh ? 0.70f : 0.84f;
        float high = harsh ? 1.22f : 1.10f;
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float n = Sample(coarse, lattice, u, v) * 0.65f
                        + Sample(fine, lattice * 2, u, v) * 0.35f;
                if (harsh) n = Mathf.Pow(n, 1.6f);

                float shade = Mathf.Lerp(low, high, n);
                pixels[y * size + x] = new Color(shade, shade, shade, 1f);
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    Vector3 CellToWorld(Vector2Int cell)
    {
        float half = theme.gridSize * theme.cellSize * 0.5f;
        return new Vector3(cell.x * theme.cellSize - half, 0f, cell.y * theme.cellSize - half);
    }

    Vector2Int ComponentCentroid(List<Vector2Int> cells)
    {
        int sx = 0, sy = 0;
        foreach (var c in cells) { sx += c.x; sy += c.y; }
        return new Vector2Int(sx / cells.Count, sy / cells.Count);
    }

    List<List<Vector2Int>> FindComponents(bool[,] solid, int n)
    {
        var visited = new bool[n, n];
        var result = new List<List<Vector2Int>>();
        var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
            {
                if (!solid[x, z] || visited[x, z]) continue;

                var comp = new List<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(new Vector2Int(x, z));
                visited[x, z] = true;

                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    comp.Add(cur);
                    foreach (var d in dirs)
                    {
                        var next = cur + d;
                        if (next.x < 0 || next.x >= n || next.y < 0 || next.y >= n) continue;
                        if (visited[next.x, next.y] || !solid[next.x, next.y]) continue;
                        visited[next.x, next.y] = true;
                        queue.Enqueue(next);
                    }
                }
                result.Add(comp);
            }
        return result;
    }

    int FindComponentContaining(List<List<Vector2Int>> components, Vector2Int cell)
    {
        for (int i = 0; i < components.Count; i++)
            if (components[i].Contains(cell))
                return i;

        Debug.LogWarning("La celda central no apareció en ningún componente sólido (no debería pasar).");
        return 0;
    }

    void ConnectWithBridges(List<List<Vector2Int>> components, float[] componentHeight, int rootIndex)
    {
        // Los extremos de cada puente se anclan a la altura real del terreno en ese punto
        // (base de la isla más su cerro), si no quedarían flotando o enterrados.
        var centroidsWorld = new Vector3[components.Count];
        for (int i = 0; i < components.Count; i++)
        {
            Vector2Int centroid = ComponentCentroid(components[i]);
            centroidsWorld[i] = CellToWorld(centroid) + Vector3.up * CellHeight(centroid, componentHeight[i]);
        }

        var connected = new HashSet<int> { rootIndex };
        var remaining = new List<int>();
        for (int i = 0; i < components.Count; i++)
            if (i != rootIndex) remaining.Add(i);

        int bridgesBuilt = 0;

        while (remaining.Count > 0)
        {
            float bestDist = float.MaxValue;
            int bestFromConnected = -1, bestRemainingSlot = -1;

            for (int ri = 0; ri < remaining.Count; ri++)
            {
                int r = remaining[ri];
                foreach (int c in connected)
                {
                    float d = Vector3.Distance(centroidsWorld[c], centroidsWorld[r]);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestFromConnected = c;
                        bestRemainingSlot = ri;
                    }
                }
            }

            int chosen = remaining[bestRemainingSlot];
            remaining.RemoveAt(bestRemainingSlot);
            connected.Add(chosen);

            if (bestDist > theme.maxJumpRange)
            {
                BuildBridge(centroidsWorld[bestFromConnected], centroidsWorld[chosen]);
                bridgesBuilt++;
            }
        }

        if (bridgesBuilt > 0)
            Debug.Log(bridgesBuilt + " puente(s) de plataformas generados para conectar zonas lejanas.");
    }

    void BuildBridge(Vector3 from, Vector3 to)
    {
        float dist = Vector3.Distance(from, to);
        int steps = Mathf.CeilToInt(dist / theme.maxJumpRange);

        for (int s = 1; s < steps; s++)
        {
            float t = (float)s / steps;
            Vector3 pos = Vector3.Lerp(from, to, t);

            // Los puentes siempre son de piedra: se leen como camino construido, no como terreno.
            BuildGroundCell("Bridge", pos, theme.cellSize * theme.platformPadding, 0.5f, true, s * 211 + steps * 17);
            walkableCellPositions.Add(pos + Vector3.up * 0.55f);
        }
    }
}
