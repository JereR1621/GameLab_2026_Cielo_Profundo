using System.Collections.Generic;
using UnityEngine;

public static class LowPolyMeshFactory
{
    // Bipirámide irregular: cada cara tiene sus propios vértices (no se comparten
    // entre caras vecinas), así RecalculateNormals da facetas planas y duras
    // en vez de sombreado suave.
    public static Mesh CreateCrystal(int sides, float height, float radius, int seed)
    {
        var rng = new System.Random(seed);

        Vector3 top = new Vector3(0f, height * 0.5f, 0f);
        Vector3 bottom = new Vector3(0f, -height * 0.5f, 0f);

        var ring = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = (i / (float)sides) * Mathf.PI * 2f;
            float jitter = 1f + (float)(rng.NextDouble() - 0.5) * 0.3f;
            float r = radius * jitter;
            float y = ((float)rng.NextDouble() - 0.5f) * height * 0.15f;
            ring[i] = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
        }

        var verts = new List<Vector3>();
        var tris = new List<int>();

        void AddTri(Vector3 a, Vector3 b, Vector3 c)
        {
            int baseIdx = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
        }

        for (int i = 0; i < sides; i++)
        {
            Vector3 a = ring[i];
            Vector3 b = ring[(i + 1) % sides];
            AddTri(top, b, a);
            AddTri(bottom, a, b);
        }

        var mesh = new Mesh { name = "Crystal" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Esfera facetada de latitud/longitud. Con pocos segmentos queda bien angulosa,
    // que es lo que hace falta para un casco en este estilo. Cada triángulo lleva sus
    // propios vértices para que el sombreado sea plano y no suave.
    public static Mesh CreateFacetedSphere(int segments, int rings, float radius)
    {
        segments = Mathf.Max(4, segments);
        rings = Mathf.Max(3, rings);

        var verts = new List<Vector3>();
        var tris = new List<int>();

        void AddTri(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        Vector3 PointAt(int ring, int seg)
        {
            float phi = Mathf.PI * ring / rings;          // 0 arriba, PI abajo
            float theta = 2f * Mathf.PI * seg / segments;
            return new Vector3(
                Mathf.Sin(phi) * Mathf.Cos(theta),
                Mathf.Cos(phi),
                Mathf.Sin(phi) * Mathf.Sin(theta)) * radius;
        }

        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                Vector3 a = PointAt(r, s);
                Vector3 b = PointAt(r, s + 1);
                Vector3 c = PointAt(r + 1, s + 1);
                Vector3 d = PointAt(r + 1, s);

                // Los casquetes degeneran en triángulo: ahí un vértice se repite.
                if (r != 0) AddTri(a, b, c);
                if (r != rings - 1) AddTri(a, c, d);
            }
        }

        var mesh = new Mesh { name = "FacetedSphere" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Trozo de suelo de tamaño unitario (-0.5..0.5). Se arma con tres anillos de
    // puntos en vez de esquinas de caja, así el contorno queda irregular y no se
    // lee como un cubo. La tapa se mantiene bastante plana para que caminar encima
    // sea cómodo; lo que se afina hacia abajo son los bordes.
    //
    // rocky = true  -> aristas duras, más alto, más desprolijo (piedra)
    // rocky = false -> perfil bajo y parejo, contorno más redondeado (tierra)
    public static Mesh CreateGroundChunk(int seed, bool rocky)
    {
        var rng = new System.Random(seed);
        float J(float amount) => (float)(rng.NextDouble() - 0.5) * 2f * amount;

        int sides = rocky ? 5 + rng.Next(3) : 6 + rng.Next(3);
        float radialJitter = rocky ? 0.11f : 0.05f;
        float topJitter = rocky ? 0.13f : 0.04f;
        float crownLift = rocky ? 0.06f + (float)rng.NextDouble() * 0.12f : 0.02f;
        float topInset = rocky ? 0.34f : 0.40f;

        var topRing = new Vector3[sides];
        var midRing = new Vector3[sides];
        var bottomRing = new Vector3[sides];

        for (int i = 0; i < sides; i++)
        {
            float angle = (i / (float)sides) * Mathf.PI * 2f;
            float dirX = Mathf.Cos(angle);
            float dirZ = Mathf.Sin(angle);
            float wobble = J(radialJitter);

            float midR = 0.5f + wobble;
            float topR = topInset + wobble * 0.6f;
            float bottomR = 0.40f + wobble * 0.5f;

            topRing[i] = new Vector3(dirX * topR, 0.5f + J(topJitter), dirZ * topR);
            midRing[i] = new Vector3(dirX * midR, 0.12f + J(0.06f), dirZ * midR);
            bottomRing[i] = new Vector3(dirX * bottomR, -0.5f, dirZ * bottomR);
        }

        Vector3 crown = new Vector3(J(0.06f), 0.5f + crownLift, J(0.06f));
        Vector3 keel = new Vector3(0f, -0.5f, 0f);

        var verts = new List<Vector3>();
        var tris = new List<int>();

        void AddTri(Vector3 a, Vector3 b, Vector3 c)
        {
            int baseIdx = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
        }

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;

            AddTri(crown, topRing[next], topRing[i]);

            AddTri(topRing[i], topRing[next], midRing[next]);
            AddTri(topRing[i], midRing[next], midRing[i]);

            AddTri(midRing[i], midRing[next], bottomRing[next]);
            AddTri(midRing[i], bottomRing[next], bottomRing[i]);

            AddTri(keel, bottomRing[i], bottomRing[next]);
        }

        var mesh = new Mesh { name = rocky ? "RockChunk" : "DirtChunk" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
