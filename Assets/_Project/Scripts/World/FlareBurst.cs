using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// El fogonazo de la bengala. Hace tres cosas a la vez, y las tres importan:
// aturde a lo que tiene encima, llama a todo lo que esté dentro del radio, y al
// final las dispersa en direcciones distintas para que no queden amontonadas.
public class FlareBurst : MonoBehaviour
{
    [SerializeField] float stunRadius = 6f;
    [SerializeField] float stunDuration = 7f;

    [Tooltip("Radio de llamada. Acotado a propósito: no despierta a todo el mapa.")]
    [SerializeField] float callRadius = 55f;
    [Tooltip("Cuánto se quedan revisando el punto UNA VEZ QUE LLEGAN. El viaje se suma aparte, según lo lejos que estuviera cada una.")]
    [SerializeField] float attentionDuration = 9f;

    [Tooltip("Separación deseada entre dos arañas que llegan al mismo fogonazo. El anillo se calcula con esto y con cuántas fueron llamadas; si va una sola, no hay anillo y va al centro.")]
    [SerializeField] float approachSpacing = 2.5f;

    [Tooltip("Radio del corral alrededor del fogonazo. Ninguna araña llamada puede quedar fuera de esto mientras dure la atención, así que todas se ven DENTRO del círculo de luz y no alrededor.")]
    [SerializeField] float gatherRadius = 5f;

    [Tooltip("Margen sobre el tiempo de viaje calculado. La araña no corre siempre a la misma velocidad, así que sin margen llega justo tarde.")]
    [SerializeField] float travelMargin = 1.5f;

    [SerializeField] float retreatDuration = 7f;

    [Header("Luz")]
    [Tooltip("Radio del círculo que ilumina en el suelo.")]
    [SerializeField] float lightRange = 38f;
    [SerializeField] float lightIntensity = 22f;
    [SerializeField] float burnSeconds = 16f;
    [Tooltip("Cuánto dura el apagado final, contado desde el total.")]
    [SerializeField] float fadeSeconds = 3.5f;

    Light glow;
    float ignitedAt;
    readonly List<SpiderCreature> called = new List<SpiderCreature>();

    public void Detonate()
    {
        var nearby = new List<SpiderCreature>();
        foreach (var spider in SpiderCreature.All)
        {
            if (spider == null) continue;
            float d = Vector3.Distance(spider.transform.position, transform.position);

            if (d <= stunRadius) spider.Stun(stunDuration);
            else if (d <= callRadius) nearby.Add(spider);
        }

        // El anillo existe solo para que no se peleen por el mismo metro cuadrado, así
        // que se calcula con cuántas vinieron: se busca que queden a approachSpacing
        // una de otra, y de ahí sale el radio. Con UNA sola araña el radio es cero y va
        // al fogonazo mismo, que es lo que uno espera al tirar una bengala. Antes era
        // un anillo fijo de 4,5 y se plantaba a cinco metros aunque fuera la única.
        //
        // Y se topa contra el corral: con muchas arañas el anillo crecía sin límite y
        // las de más afuera terminaban en el borde del círculo de luz o directamente
        // fuera. Antes que se salgan del charco, prefiero que se aprieten.
        float ring = nearby.Count <= 1
            ? 0f
            : Mathf.Min(nearby.Count * approachSpacing / (Mathf.PI * 2f), gatherRadius * 0.6f);

        // La atención se cuenta desde que LLEGA, no desde el fogonazo. Antes era un
        // plazo fijo para todo el radio de llamada, y como a paso de caza no se cubren
        // 55 unidades en 9 segundos, cualquiera que estuviera lejos recibía la orden de
        // dispersarse a mitad de camino y se daba vuelta antes de llegar.
        float longest = 0f;

        for (int i = 0; i < nearby.Count; i++)
        {
            float angle = (i / (float)Mathf.Max(1, nearby.Count)) * Mathf.PI * 2f
                          + Random.Range(-0.35f, 0.35f);
            Vector3 spot = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;

            float distance = Vector3.Distance(nearby[i].transform.position, spot);
            float speed = Mathf.Max(0.1f, nearby[i].HuntSpeed);
            float duration = distance / speed * travelMargin + attentionDuration;

            nearby[i].Distract(spot, duration);
            nearby[i].ConfineTo(transform.position, gatherRadius);
            called.Add(nearby[i]);

            longest = Mathf.Max(longest, duration);
        }

        Debug.Log($"Bengala: {called.Count} araña(s) llamadas dentro de {callRadius}, " +
                  $"anillo {ring:0.0}, la más lejana tarda {longest:0.0}s.");
        StartCoroutine(BurnAndDisperse(longest));
    }

    // Se espera a la última en llegar antes de dispersarlas: si se dispersara en el
    // plazo de la primera, las de atrás nunca tocarían el fogonazo.
    IEnumerator BurnAndDisperse(float attentionWindow)
    {
        yield return new WaitForSeconds(attentionWindow);

        // Se van cada una para su lado, en ángulos repartidos. Si se retiraran todas
        // en la misma dirección se irían en bloque, que se ve peor que amontonadas.
        int alive = 0;
        foreach (var s in called) if (s != null) alive++;

        int index = 0;
        foreach (var spider in called)
        {
            if (spider == null) continue;

            float angle = (index / (float)Mathf.Max(1, alive)) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
            spider.Retreat(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)), retreatDuration);
            index++;
        }

        // Si la última en llegar tardó más de lo que dura la bengala, el objeto ya está
        // apagado (la envolvente de la luz llega a cero en burnSeconds) pero sigue vivo
        // hasta acá. No se ve, y es lo que mantiene viva a esta corrutina.
        yield return new WaitForSeconds(Mathf.Max(0f, burnSeconds - attentionWindow));
        Destroy(gameObject);
    }

    void Awake()
    {
        ignitedAt = Time.time;

        var lightGO = new GameObject("LuzBengala");
        lightGO.transform.SetParent(transform, false);
        // Levantada del piso: pegada al suelo la atenuación se come el círculo y
        // solo se ve una mancha chica justo debajo.
        lightGO.transform.localPosition = new Vector3(0f, 0.9f, 0f);

        glow = lightGO.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.58f, 0.28f);
        glow.range = lightRange;
        glow.intensity = lightIntensity;
    }

    // El titileo va desde que cae, no solo al final: una bengala encendida no
    // alumbra parejo. Y el apagado empieza recién en los últimos segundos.
    void Update()
    {
        if (glow == null) return;

        float age = Time.time - ignitedAt;
        float fade = burnSeconds - fadeSeconds;
        float envelope = age <= fade ? 1f : Mathf.Clamp01(1f - (age - fade) / fadeSeconds);

        float flicker = 0.82f + 0.18f * Mathf.PerlinNoise(Time.time * 9f, 0.5f);
        glow.intensity = lightIntensity * envelope * flicker;
    }
}
