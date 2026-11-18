using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Selección de props de los packs importados y su puesta a punto.
//
// Solo entra lo que sirve para ambientar la superficie de un cuerpo. Queda afuera
// a propósito:
//   · SP_Ground0X  — son planchas de suelo, y el suelo lo genera el ruido.
//   · SP_Planet    — es un planeta entero, decorado de fondo, no un prop.
//   · SP_Mountain  — son piezas de terreno enormes; se cruzarían con los cerros
//                    que ya genera el mapa en vez de sumarse a ellos.
//   · PolyAngel    — es una base espacial (domos, torre, cohetes). Eso es el pase
//                    de estructuras, otra cosa, y además usa su propia paleta.
public static class PropLibrary
{
    const string Pack = "Assets/Free Demo of Low Poly Space Alien Worlds 3D Asset Pack";
    const string PrefabRoot = Pack + "/Prefabs/Space Forest";
    const string ModelRoot = Pack + "/Models/Space Forest";
    const string PaletteTexture = Pack + "/Texture/tex.png";
    const string MaterialPath = "Assets/_Project/Materials/PS1_Props.mat";

    // Se llama desde el builder de la escena. Deja los assets del pack listos y
    // devuelve los grupos ya armados.
    public static PropScatter.PropGroup[] BuildGroups(out Material material)
    {
        PreparePalette();
        material = PrepareMaterial();

        var groups = new List<PropScatter.PropGroup>
        {
            Group("Árboles", "SP_Trees", "SP_Tree", 1, 4,
                  peso: 1f, celdasPorProp: 55f, escala: new Vector2(0.8f, 1.4f),
                  inclinacion: 4f, bloquea: true, separacion: 8f),

            Group("Plantas", "SP_Plants", "SP_Plant", 1, 9,
                  peso: 1f, celdasPorProp: 14f, escala: new Vector2(0.7f, 1.3f),
                  inclinacion: 8f, bloquea: false, separacion: 2.5f),

            Group("Rocas", "SP_Rocks", "SP_Rock", 1, 9,
                  peso: 1f, celdasPorProp: 30f, escala: new Vector2(0.7f, 1.5f),
                  inclinacion: 10f, bloquea: true, separacion: 6f),

            // Las piedras miden un centímetro de alto: no son piedras sueltas sino
            // manchas planas de grava. Van sin inclinación porque no hay volumen que
            // inclinar — ladeadas solo se hunden de canto y desaparecen.
            Group("Piedras", "SP_Stones", "SP_Stone", 1, 2,
                  peso: 1f, celdasPorProp: 18f, escala: new Vector2(1.5f, 3.5f),
                  inclinacion: 0f, bloquea: false, separacion: 2f),

            // Puntos de referencia: pocos, grandes y visibles de lejos. Sirven para
            // orientarse en un mapa donde la bruma borra el horizonte. De ahí la
            // escala alta: el más grande mide 1,6, y así llega a los 5.
            Group("Cristales", "SP_Crystals", "SP_Crystal", 1, 2,
                  peso: 1f, celdasPorProp: 120f, escala: new Vector2(1.8f, 3.2f),
                  inclinacion: 6f, bloquea: true, separacion: 14f),
        };

        // La antena va suelta porque es una sola pieza y cuenta algo distinto: es lo
        // único del lote que es equipo, no paisaje. Rarísima a propósito.
        var antenna = LoadPrefab(PrefabRoot + "/SP_Sci-fi_Antenna.prefab");
        FixNormals(ModelRoot + "/SP_Sci-fi_Antenna.fbx");
        if (antenna != null)
        {
            groups.Add(new PropScatter.PropGroup
            {
                nombre = "Restos",
                prefabs = new[] { antenna },
                peso = 1f,
                celdasPorProp = 400f,
                escala = new Vector2(0.9f, 1.2f),
                inclinacion = 3f,
                bloquea = true,
                separacion = 40f,
            });
        }

        return groups.ToArray();
    }

    // Diagnóstico: imprime el alto real de cada prop. Sirve para saber si los rangos
    // de escala de arriba tienen sentido contra una celda de terreno de 3 unidades.
    [MenuItem("Cielo Profundo/Diagnóstico: medir props")]
    public static void MeasureProps()
    {
        var groups = BuildGroups(out _);
        foreach (var group in groups)
        {
            foreach (var prefab in group.prefabs)
            {
                if (prefab == null) continue;

                var renderers = prefab.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length == 0) continue;

                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                Debug.Log($"MEDIDA {group.nombre}/{prefab.name}: " +
                          $"alto {bounds.size.y:0.00}  ancho {bounds.size.x:0.00} x {bounds.size.z:0.00}  " +
                          $"pivote a {(-bounds.min.y):0.00} de la base");
            }
        }
    }

    // Las rocas sueltas, para vestir el labio de los cráteres. Se reutilizan las mismas
    // del scatter: ya están importadas, ya tienen las normales facetadas y ya usan la
    // paleta del pack.
    public static GameObject[] RockPrefabs()
    {
        var rocas = new List<GameObject>();

        for (int i = 1; i <= 9; i++)
        {
            var prefab = LoadPrefab($"{PrefabRoot}/SP_Rocks/SP_Rock{i:00}.prefab");
            if (prefab != null) rocas.Add(prefab);
        }

        return rocas.ToArray();
    }

    static PropScatter.PropGroup Group(string nombre, string folder, string prefix, int from, int to,
                                       float peso, float celdasPorProp, Vector2 escala,
                                       float inclinacion, bool bloquea, float separacion)
    {
        var prefabs = new List<GameObject>();
        for (int i = from; i <= to; i++)
        {
            string file = $"{prefix}{i:00}";
            FixNormals($"{ModelRoot}/{folder}/{file}.fbx");

            var prefab = LoadPrefab($"{PrefabRoot}/{folder}/{file}.prefab");
            if (prefab != null) prefabs.Add(prefab);
        }

        return new PropScatter.PropGroup
        {
            nombre = nombre,
            prefabs = prefabs.ToArray(),
            peso = peso,
            celdasPorProp = celdasPorProp,
            escala = escala,
            inclinacion = inclinacion,
            bloquea = bloquea,
            separacion = separacion,
        };
    }

    static GameObject LoadPrefab(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) Debug.LogWarning("PropLibrary: no se encontró " + path);
        return prefab;
    }

    // Los packs vienen con normales suavizadas. Recalculadas con ángulo 0 cada cara
    // queda plana, que es como se ve todo lo demás del juego.
    static void FixNormals(string fbxPath)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) return;

        if (importer.importNormals == ModelImporterNormals.Calculate &&
            Mathf.Approximately(importer.normalSmoothingAngle, 0f)) return;

        importer.importNormals = ModelImporterNormals.Calculate;
        importer.normalSmoothingAngle = 0f;
        importer.SaveAndReimport();
    }

    // La textura es una grilla de cuadrados de color, uno por material. Con filtrado
    // bilineal y mipmaps los cuadrados vecinos se mezclan y aparecen colores que no
    // existen en la paleta, sobre todo de lejos. Point y sin mipmaps lo evita, y de
    // paso el aliasing que queda es el correcto para el estilo.
    static void PreparePalette()
    {
        var importer = AssetImporter.GetAtPath(PaletteTexture) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("PropLibrary: no se encontró la paleta en " + PaletteTexture);
            return;
        }

        if (importer.filterMode == FilterMode.Point && !importer.mipmapEnabled) return;

        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    static Material PrepareMaterial()
    {
        Directory.CreateDirectory("Assets/_Project/Materials");
        AssetDatabase.Refresh();

        var shader = Shader.Find("Cielo Profundo/PS1 Textured");
        if (shader == null)
        {
            Debug.LogError("PropLibrary: falta el shader 'Cielo Profundo/PS1 Textured'.");
            return null;
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        // Se reescribe siempre, no solo al crearlo: si no, tocar estos valores acá no
        // tendría ningún efecto sobre un material que ya existe.
        mat.shader = shader;
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteTexture));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_SnapResolution", 140f);
        mat.SetFloat("_AmbientFloor", 0.16f);
        EditorUtility.SetDirty(mat);

        return mat;
    }
}
