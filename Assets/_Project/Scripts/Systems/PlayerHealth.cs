using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [SerializeField] int maxHearts = 3;
    [Tooltip("Tiempo intocable después de perder un corazón. Sin esto la araña vuelve a atraparte al instante y te vacía la vida en segundos.")]
    [SerializeField] float invulnerabilityDuration = 3.5f;

    float invulnerabilityTimer;

    public int MaxHearts => maxHearts;
    public int Hearts { get; private set; }
    public bool IsInvulnerable => invulnerabilityTimer > 0f;
    public bool IsDead => Hearts <= 0;

    public event Action OnChanged;
    public event Action OnDeath;

    void Awake()
    {
        Instance = this;

        // El techo lo fija la expedición: comprar corazones sube la capacidad, no cura.
        // Siempre bajas con el traje entero, así que la vida no se arrastra entre
        // descensos — arrastrarla haría que una mala jornada condenara a la siguiente.
        maxHearts = Expedition.MaxHearts;
        Hearts = maxHearts;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (invulnerabilityTimer > 0f) invulnerabilityTimer -= Time.deltaTime;
    }

    public void LoseHeart()
    {
        if (IsDead) return;

        Hearts--;
        invulnerabilityTimer = invulnerabilityDuration;
        OnChanged?.Invoke();

        if (IsDead) OnDeath?.Invoke();
    }
}
