using UnityEngine;

// Un puesto donde esconderse: hoy, el fondo de un cráter.
//
// Lleva su propia luz de aviso porque si no, un cráter y un montón de piedras se ven
// idénticos. La luz es lo que convierte un accidente del terreno en una decisión: la ves
// de lejos y sabes hacia dónde correr cuando la oyes venir.
//
// Verde a propósito, y es el único verde del juego. El cian es la interfaz, el ámbar
// son los avisos y las balizas, el rojo es daño, y las fichas usan sus propios colores.
// Un color que no aparece en ningún otro lado no hay que aprenderlo dos veces.
public class HideSpot : MonoBehaviour
{
    [SerializeField] float interactRange = 2.4f;

    [Header("Aviso")]
    [SerializeField] Color glow = new Color(0.35f, 0.95f, 0.68f);
    [SerializeField] float lightRange = 6f;
    [SerializeField] float lightIntensity = 2.6f;
    [SerializeField] float frameRate = 12f;

    // Dónde se para el jugador y hacia dónde mira. Los fija el cráter al crearlo,
    // porque es él quien sabe dónde queda su fondo.
    public Vector3 StandPosition { get; private set; }
    public Vector3 FacingDirection { get; private set; } = Vector3.forward;

    // Altura del canto del bloque contra el que te apoyas, medida desde los pies. Es el
    // parámetro con el que se deforma la pose de asomarse y la curva del salto: cada
    // bloque del labio mide distinto y una animación fija no serviría para todos.
    public float EdgeOffset { get; private set; } = 1.4f;

    Light lamp;
    int lastFrame = -1;

    // Dentro de un cráter la orientación no importa —el labio rodea— así que se pide no
    // girar al jugador: darle un tirón de cámara al meterse se siente peor que dejarlo
    // mirando hacia donde venía.
    public bool KeepFacing { get; private set; }

    public void Configure(Vector3 standPosition, Vector3 facing, Material markerMaterial,
                          bool keepFacing = false, float edgeWorldY = float.NaN)
    {
        StandPosition = standPosition;
        FacingDirection = facing.sqrMagnitude < 0.001f ? Vector3.forward : facing.normalized;
        KeepFacing = keepFacing;

        if (!float.IsNaN(edgeWorldY))
            EdgeOffset = Mathf.Clamp(edgeWorldY - standPosition.y, 0.8f, 2.4f);

        BuildMarker(markerMaterial);
    }

    void Update()
    {
        Pulse();

        var hiding = PlayerHiding.Instance;
        if (hiding == null || hiding.IsHiding) return;

        Vector3 delta = hiding.transform.position - transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance > interactRange) return;

        hiding.Prompt?.Request("esconderse", distance);

        // No en el cuadro en que acaba de salir: si no, la misma E lo devuelve adentro.
        if (!hiding.ToggledThisFrame && Input.GetKeyDown(KeyCode.E)) hiding.Enter(this);
    }

    // Late a saltos de 12 fps, como todo lo demás. Una luz que respira suave se ve de
    // otro juego, y además a saltos se distingue mejor del resplandor fijo de las fichas.
    void Pulse()
    {
        if (lamp == null) return;

        int frame = Mathf.FloorToInt(Time.time * frameRate);
        if (frame == lastFrame) return;
        lastFrame = frame;

        float t = (Mathf.Sin(Time.time * 2.2f) + 1f) * 0.5f;
        lamp.intensity = lightIntensity * Mathf.Lerp(0.55f, 1f, t);
    }

    void BuildMarker(Material markerMaterial)
    {
        // Un poste corto en el fondo. Chico: tiene que señalar el sitio, no tapar el labio
        // ni estorbar al meterse.
        var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.name = "Baliza";
        post.transform.SetParent(transform, false);
        post.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        post.transform.localScale = new Vector3(0.16f, 0.9f, 0.16f);
        Destroy(post.GetComponent<Collider>());

        if (markerMaterial != null) post.GetComponent<Renderer>().sharedMaterial = markerMaterial;

        var lampGO = new GameObject("LuzEscondite");
        lampGO.transform.SetParent(transform, false);
        lampGO.transform.localPosition = new Vector3(0f, 1f, 0f);

        lamp = lampGO.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = glow;
        lamp.range = lightRange;
        lamp.intensity = lightIntensity;
        lamp.shadows = LightShadows.None;
    }
}
