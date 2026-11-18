using UnityEngine;

// Proximidad + E para abrir cualquier panel de la nave. Uno solo sirve para la mesa
// de destinos, la tienda y la pantalla de misión: cambia la referencia y la etiqueta.
public class PanelTerminal : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] InteractionPromptUI prompt;
    [SerializeField] HoloPanel panel;
    [SerializeField] string actionLabel = "abrir";
    [SerializeField] float interactRange = 3f;

    [Tooltip("Signo de exclamación flotante. Se apaga mientras el panel está abierto.")]
    [SerializeField] GameObject marker;

    void Update()
    {
        if (panel == null || player == null) return;

        bool uiUp = HoloPanel.AnyOpen || InventoryUI.CatalogOpen;
        if (marker != null) marker.SetActive(!uiUp);

        // Se calla con CUALQUIER panel abierto, no solo con el suyo. Al confirmar
        // destino, el selector se cierra y se abre la esfera armilar: para este
        // terminal su panel volvía a estar cerrado, así que ofrecía "E para abrir" por
        // encima del minijuego y la misma E que confirmaba una jugada reabría la mesa.
        if (uiUp) return;

        // Distancia en el plano: la pantalla de misión está alta en la pared, y
        // contando la altura nunca te acercabas lo suficiente para dispararla.
        Vector3 delta = player.position - transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;

        if (distance > interactRange) return;

        // Se pide el cartel en vez de mostrarlo: si estás cerca de dos puestos a la
        // vez, gana el más cercano y ninguno le apaga el cartel al otro.
        prompt?.Request(actionLabel, distance);

        // Se ignora el frame en que el panel acaba de cerrarse: si el panel corrió
        // antes que esto, la misma pulsación de E que lo cerró seguiría activa acá y
        // lo reabriría de inmediato.
        if (Time.frameCount == panel.LastToggleFrame) return;

        if (Input.GetKeyDown(KeyCode.E)) panel.Open();
    }
}
