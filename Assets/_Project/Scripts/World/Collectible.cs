using System.Collections.Generic;
using UnityEngine;

// El vaivén no vive acá: lo hace BobSpin, el mismo componente que llevan los signos
// de interacción de la nave. Así los dos flotan exactamente igual.
[RequireComponent(typeof(BobSpin))]
public class Collectible : MonoBehaviour
{
    public static readonly List<Collectible> All = new List<Collectible>();

    [SerializeField] CatalogEntry entry;

    public CatalogEntry Entry => entry;
    public Transform Visual { get; private set; }

    BobSpin motion;

    public void SetEntry(CatalogEntry newEntry) => entry = newEntry;

    public void SetVisual(Transform visual)
    {
        Visual = visual;
        visual.SetParent(transform, false);
    }

    // Durante la secuencia de recogida la maneja ItemPickupSequence, no el vaivén.
    public void SuspendIdleMotion()
    {
        if (motion != null) motion.enabled = false;
    }

    void Awake()
    {
        motion = GetComponent<BobSpin>();
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }
}
