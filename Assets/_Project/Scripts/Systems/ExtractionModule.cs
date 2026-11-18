using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// El módulo con el que bajaste. Queda en el punto de aterrizaje y es la única forma
// de volver a la nave.
//
// Exige el catálogo COMPLETO. Sin todas las piezas del cuerpo, el módulo no despega.
//
// La duda con esta regla es si puede dejar la partida trabada, y no puede: las fichas
// solo nacen en celdas pisables, el generador une todas las islas con puentes, el
// radar enseña dónde está cada una, y el total del contador se calcula sobre las que
// de verdad se colocaron y no sobre las que se pidieron. No hay forma de que quede
// una inalcanzable. Y si aun así algo saliera mal, la pausa deja salir al menú.
//
// Lo que sí importa: las fichas se registran ACÁ, al despegar. Morir en la superficie
// pierde la jornada entera. Es lo que hace que la araña tenga peso.
public class ExtractionModule : MonoBehaviour
{
    [SerializeField] Transform player;
    [SerializeField] InteractionPromptUI prompt;
    [SerializeField] NoiseTerrainGenerator generator;
    [SerializeField] float interactRange = 4f;
    [SerializeField] string lobbySceneName = "Ship_Lobby";

    [Tooltip("El cierre de la ruta. Se reproduce en vez de volver a la nave cuando despegas del último cuerpo habiendo transmitido algo.")]
    [SerializeField] EndingUI ending;

    [Header("Pago")]
    [Tooltip("Créditos por cada ficha transmitida. Es la única fuente de dinero del juego.")]
    [SerializeField] int creditsPerItem = 30;

    [Tooltip("Prima de cierre de asignación. Se cobra en todo despegue, porque despegar ya exige el catálogo completo.")]
    [SerializeField] int completionBonus = 60;

    bool leaving;

    // Se planta donde el generador dejó el suelo asegurado, que es exactamente donde
    // apareces. Si se colocara por posición fija podría quedar en el vacío.
    //
    // En OnEnable y no en Start: el generador arma el terreno en SU Start, y todos los
    // OnEnable corren antes que cualquier Start. Suscribirse en Start sería una carrera.
    void OnEnable()
    {
        if (generator != null) generator.OnTerrainReady += PlaceAtLandingSite;
    }

    void OnDisable()
    {
        if (generator != null) generator.OnTerrainReady -= PlaceAtLandingSite;
    }

    void PlaceAtLandingSite(Vector3 spawnPoint)
    {
        // Corrido del centro, y bastante: el módulo es ancho y si quedara cerca del
        // punto de aparición abrirías los ojos con una pata encima.
        Vector3 target = spawnPoint + new Vector3(6.5f, 0f, 3.2f);

        // Y apoyado en el suelo de SU sitio, no en el del punto de aparición.
        //
        // Flotaba por dos motivos que se sumaban. Uno: GroundSpawnPoint no es la cara
        // del terreno, trae incorporado el margen desde el que se suelta al jugador, y
        // el módulo heredaba ese margen. Dos: corrido siete unidades cae sobre otra
        // celda, y con los cerros y el temblor de esquinas esa celda casi nunca está a
        // la misma altura.
        if (generator != null && AlturaDelSuelo(target, out float sueloY))
            target.y = sueloY;

        transform.position = target;
    }

    // La celda pisable más cercana en el plano. Se consulta la lista y no un rayo hacia
    // abajo porque esto corre en cuanto el terreno avisa que está listo, y los colliders
    // pueden no haberse registrado todavía.
    bool AlturaDelSuelo(Vector3 point, out float groundY)
    {
        groundY = point.y;

        var cells = generator.WalkableCellPositions;
        if (cells == null || cells.Count == 0) return false;

        float mejor = float.MaxValue;
        bool hay = false;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3 delta = cells[i] - point;
            delta.y = 0f;

            float d = delta.sqrMagnitude;
            if (d >= mejor) continue;

            mejor = d;
            groundY = cells[i].y;
            hay = true;
        }

        return hay;
    }

    void Update()
    {
        if (leaving || player == null) return;

        // Con la araña encima, E es para zafarse. Sin este control, forcejear al lado
        // del módulo te despegaba del planeta a mitad del forcejeo. Es el mismo
        // resguardo que ya tenía PlayerInteraction para no recoger objetos de rebote.
        if (SpiderCatchSequence.Instance != null && SpiderCatchSequence.Instance.IsPlaying) return;

        Vector3 delta = player.position - transform.position;
        delta.y = 0f;
        if (delta.magnitude > interactRange) return;

        int carried = Inventory.Instance != null ? Inventory.Instance.Collected.Count : 0;
        int total = Inventory.Instance != null ? Inventory.Instance.Total : 0;

        // El módulo no despega con el catálogo incompleto, y el cartel dice cuántas
        // faltan en vez de solo negarse. Un botón que no responde se lee como roto;
        // uno que te dice qué falta se lee como una regla.
        bool completo = total > 0 && carried >= total;

        prompt?.Request(completo
            ? $"transmitir y despegar  ({carried}/{total})"
            : $"faltan {total - carried} de {total}  ·  el módulo no despega incompleto",
            delta.magnitude);

        if (completo && Input.GetKeyDown(KeyCode.E)) Leave();
    }

    void Leave()
    {
        leaving = true;

        var ids = new List<string>();
        if (Inventory.Instance != null)
            foreach (var entry in Inventory.Instance.Collected)
                if (entry != null) ids.Add(entry.name);

        Expedition.Commit(ids);

        // Paralax paga por pieza, más el cierre de la asignación. Como no se despega sin
        // el catálogo entero, las dos partes se cobran siempre: el pago varía solo con el
        // tamaño del catálogo del cuerpo, y los cuerpos de los últimos tramos traen más
        // piezas. Bajar más lejos paga más.
        int total = Inventory.Instance != null ? Inventory.Instance.Total : 0;
        int pago = ids.Count * creditsPerItem + completionBonus;

        Expedition.Credits += pago;

        // Si este era el último cuerpo hay que preguntarlo ANTES de avanzar: Advance()
        // deja el índice donde está cuando ya no hay siguiente, así que después de
        // llamarlo no se distingue el final de la ruta de cualquier otro despegue.
        bool ultimoCuerpo = !RouteDatabase.HasNext;

        // La ruta avanza siempre que se llegue hasta aquí, porque llegar hasta aquí ya
        // exige el catálogo completo: Update no deja pulsar E sin todas las piezas.
        RouteDatabase.Advance();
        Expedition.ResetSurvey();

        Debug.Log($"Despegue de {(PlanetSession.Instance?.Body?.nombre ?? "cuerpo sin nombre")}: " +
                  $"catálogo completo, {ids.Count} de {total} fichas, {pago} créditos. " +
                  $"Ruta en el cuerpo {RouteDatabase.CurrentIndex + 1}, {RouteDatabase.UnlockedCount} abiertos.");

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Fin de la ruta: el cierre se reproduce aquí mismo, sobre el planeta, y él se
        // encarga de llevar al menú. Despegar con las manos vacías del último cuerpo no
        // cuenta como terminarla, igual que no cuenta para avanzar en cualquier otro.
        if (ultimoCuerpo && ending != null)
        {
            ending.Play();
            return;
        }

        SceneManager.LoadScene(lobbySceneName);
    }
}
