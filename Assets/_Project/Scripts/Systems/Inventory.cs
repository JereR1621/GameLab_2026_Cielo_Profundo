using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    public event Action OnChanged;

    readonly List<CatalogEntry> collected = new List<CatalogEntry>();
    public IReadOnlyList<CatalogEntry> Collected => collected;
    public int Total { get; private set; }

    // Catálogo cerrado. Lo consultan el módulo de ascenso, el aviso de la superficie y
    // la población de arañas, así que la condición vive acá y no repetida en cada uno.
    public bool IsComplete => Total > 0 && Collected.Count >= Total;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Al cambiar de escena el objeto muere pero la referencia estática seguía en pie.
    // Unity la resuelve como null igual, pero los demás singletons del proyecto se
    // limpian y este era el único que no.
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Add(CatalogEntry entry)
    {
        if (entry == null || collected.Contains(entry)) return;
        collected.Add(entry);
        OnChanged?.Invoke();
    }

    public void SetTotal(int total)
    {
        Total = total;
        OnChanged?.Invoke();
    }
}
