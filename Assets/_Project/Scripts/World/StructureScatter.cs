using System.Collections.Generic;
using UnityEngine;

public class StructureScatter : MonoBehaviour
{
    [Header("Cuántas estructuras vacías por cada una que guarda un objeto")]
    [SerializeField] float decoysPerItem = 1.6f;

    [Header("Separación mínima entre estructuras")]
    [SerializeField] float minSeparation = 16f;
    [SerializeField] float minDistanceFromSpawn = 12f;

    [Header("Qué tan seguido aparece una casa en llamas")]
    [SerializeField, Range(0f, 1f)] float burningChance = 0.28f;

    [Header("Cuánto se hunde en el terreno, para que no floten sobre los desniveles")]
    [SerializeField] float groundSink = 0.45f;

    Material wallMaterial;
    Material charredMaterial;
    Material emberMaterial;

    // La llama CollectibleSpawner directamente y no un evento: así el orden es
    // explícito y no depende de quién se inicializa primero.
    public void BuildAround(List<Vector3> itemPositions, IReadOnlyList<Vector3> walkable, Vector3 spawnPoint, Color planetColor)
    {
        BuildMaterials(planetColor);

        var placed = new List<Vector3>();

        // Cada objeto queda dentro de una estructura: nunca tirado en el suelo pelado.
        foreach (var itemPos in itemPositions)
        {
            var kind = Random.value < burningChance ? StructureKind.CasaQuemada : PickShelterKind();
            Place(kind, itemPos);
            placed.Add(itemPos);
        }

        // Y además estructuras vacías, para que ver una a lo lejos no garantice premio.
        int decoyTarget = Mathf.RoundToInt(itemPositions.Count * decoysPerItem);
        int decoysPlaced = 0;
        int attempts = 0;

        while (decoysPlaced < decoyTarget && attempts < decoyTarget * 40 && walkable.Count > 0)
        {
            attempts++;
            Vector3 candidate = walkable[Random.Range(0, walkable.Count)];

            if (Vector3.Distance(candidate, spawnPoint) < minDistanceFromSpawn) continue;
            if (TooClose(candidate, placed)) continue;

            Place(PickDecoyKind(), candidate);
            placed.Add(candidate);
            decoysPlaced++;
        }

        Debug.Log($"Estructuras: {itemPositions.Count} con objeto, {decoysPlaced} vacías.");
    }

    StructureKind PickShelterKind()
    {
        return Random.value < 0.55f ? StructureKind.Refugio : StructureKind.Ruina;
    }

    StructureKind PickDecoyKind()
    {
        float r = Random.value;
        if (r < burningChance) return StructureKind.CasaQuemada;
        if (r < 0.55f) return StructureKind.Ruina;
        if (r < 0.85f) return StructureKind.Refugio;
        return StructureKind.Torre;
    }

    void Place(StructureKind kind, Vector3 groundPosition)
    {
        var structure = StructureFactory.Build(kind, wallMaterial, charredMaterial, emberMaterial, Random.Range(0, 999999));
        structure.transform.SetParent(transform);
        structure.transform.position = groundPosition + Vector3.down * groundSink;

        // Rotación en pasos de 90°: el vano de entrada siempre queda mirando a un
        // lado limpio, y las paredes no cortan la grilla del terreno en diagonal.
        structure.transform.rotation = Quaternion.Euler(0f, 90f * Random.Range(0, 4), 0f);
    }

    bool TooClose(Vector3 candidate, List<Vector3> placed)
    {
        foreach (var p in placed)
            if (Vector3.Distance(candidate, p) < minSeparation) return true;
        return false;
    }

    void BuildMaterials(Color planetColor)
    {
        var shader = Shader.Find("Cielo Profundo/PS1 Vertex Snap");

        // Las paredes salen del color del planeta pero más apagadas y frías: se leen
        // como construidas, distintas del terreno, sin salirse de la paleta.
        wallMaterial = new Material(shader);
        wallMaterial.SetColor("_BaseColor", Color.Lerp(planetColor, new Color(0.42f, 0.40f, 0.46f), 0.62f));
        wallMaterial.SetFloat("_SnapResolution", 140f);

        charredMaterial = new Material(shader);
        charredMaterial.SetColor("_BaseColor", new Color(0.11f, 0.09f, 0.13f));
        charredMaterial.SetFloat("_SnapResolution", 140f);

        // Brasa: el coral de la paleta, el color reservado para lo que quema.
        emberMaterial = new Material(shader);
        emberMaterial.SetColor("_BaseColor", new Color(0.94f, 0.50f, 0.37f));
        emberMaterial.SetFloat("_SnapResolution", 140f);
    }
}
