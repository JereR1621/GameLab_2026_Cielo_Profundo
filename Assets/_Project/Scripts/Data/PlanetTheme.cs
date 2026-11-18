using UnityEngine;

[CreateAssetMenu(fileName = "NuevoPlaneta", menuName = "Cielo Profundo/Tema de planeta")]
public class PlanetTheme : ScriptableObject
{
    [Header("Semilla y grilla")]
    public int seed = 0;
    public int gridSize = 81;
    public float cellSize = 3f;
    [Tooltip("Cuánto más grande se construye cada piedra de puente respecto a la celda que ocupa. Solo afecta a los puentes: el terreno de las islas es una malla continua.")]
    public float platformPadding = 1.4f;

    [Tooltip("Espesor de las islas: cuánto bajan las paredes del borde hacia el vacío.")]
    public float islandThickness = 3.2f;

    [Tooltip("Cuánto se corre cada esquina de la grilla, en fracción de celda, para que el contorno de las islas no se vea cuadriculado. Por encima de 0.4 las celdas empiezan a solaparse.")]
    [Range(0f, 0.4f)] public float cornerJitter = 0.3f;

    [Header("Ruido")]
    public float noiseScale = 0.12f;
    [Range(0f, 1f)] public float solidThreshold = 0.48f;

    [Header("Clasificación: suelo vs. plataforma")]
    public int groundMinCells = 12;
    public float heightNoiseScale = 0.2f;
    public float platformHeightRange = 6f;

    [Header("Zona de aterrizaje garantizada (radio en celdas, siempre sólida)")]
    public int spawnPatchRadius = 4;

    [Header("Relieve: cerros sobre el suelo, para que se sienta un mundo y no una plataforma")]
    [Tooltip("Más chico = cerros anchos y suaves. Más grande = relieve más quebrado.")]
    public float hillNoiseScale = 0.045f;
    [Tooltip("Altura máxima de los cerros. Ojo: si el desnivel entre celdas vecinas supera el stepOffset del CharacterController, el jugador no puede subir.")]
    public float hillHeight = 6f;

    [Header("Alcance de un salto+planeo cómodo: separación máxima entre plataformas de un puente")]
    public float maxJumpRange = 8f;

    [Header("Estética")]
    public Color baseColor = new Color(0.435f, 0.788f, 0.831f);
    public Color fogColor = new Color(0.043f, 0.024f, 0.125f);
    public float fogDensity = 0.035f;
}
