using UnityEngine;

// Estelas que cruzan la ventana, viniendo de afuera hacia adentro. No hay ninguna
// nave real moviéndose: las estelas nacen lejos y avanzan hasta el plano de la
// ventana, y como la pared con el hueco las tapa desde cualquier otro ángulo, el
// efecto solo se ve a través del vidrio. Es la forma barata de vender movimiento.
//
// Cada estela tiene SU largo, SU grosor y SU velocidad, y esto es lo que separa un
// campo de estrellas de una persiana. Antes todas medían lo mismo y viajaban a la
// misma velocidad, así que se movían en bloque y se leían como barras deslizándose:
// el ojo pilla enseguida el patrón cuando todo va acompasado.
//
// Y no aparecen ni desaparecen de golpe: se estiran al entrar y se encogen al salir.
// Sin eso, con material opaco, cada estela parpadea en el borde del encuadre.
public class StarStreakField : MonoBehaviour
{
    [SerializeField] Material streakMaterial;
    [SerializeField] int count = 46;
    [SerializeField] Vector2 spread = new Vector2(9f, 5f);

    [Tooltip("Recorrido total. Las estelas nacen a media distancia por detrás y mueren a media distancia por delante, así cruzan la ventana en vez de aparecer o desaparecer dentro de ella.")]
    [SerializeField] float distance = 70f;

    [Tooltip("Velocidades. El rango ancho es lo que rompe el movimiento en bloque: las de cerca pasan rápido y las de lejos se arrastran, que además da sensación de profundidad.")]
    [SerializeField] Vector2 speedRange = new Vector2(10f, 30f);

    [SerializeField] Vector2 lengthRange = new Vector2(1.4f, 5.5f);
    [SerializeField] Vector2 thicknessRange = new Vector2(0.022f, 0.06f);

    [Tooltip("Qué fracción del recorrido ocupa el estirado de entrada y el encogido de salida.")]
    [SerializeField, Range(0.02f, 0.4f)] float fadeFraction = 0.14f;

    Transform[] streaks;
    float[] progress, speed, length, thickness;

    void Awake()
    {
        streaks = new Transform[count];
        progress = new float[count];
        speed = new float[count];
        length = new float[count];
        thickness = new float[count];

        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Estela" + i;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.GetComponent<Renderer>().sharedMaterial = streakMaterial;

            streaks[i] = go.transform;

            // Repartidas por todo el recorrido desde el principio. Arrancando todas en
            // cero, el primer par de segundos se vería la ventana vacía y después una
            // oleada entrando junta.
            progress[i] = Random.value;
            Respawn(i, keepProgress: true);
        }
    }

    void Update()
    {
        for (int i = 0; i < streaks.Length; i++)
        {
            progress[i] += Time.deltaTime * speed[i] / distance;

            if (progress[i] >= 1f)
            {
                progress[i] = 0f;
                Respawn(i, keepProgress: true);
                continue;
            }

            // Cuánto le queda al borde más cercano, de 0 a 1.
            float borde = Mathf.Clamp01(Mathf.Min(progress[i], 1f - progress[i]) / fadeFraction);

            var local = streaks[i].localPosition;
            local.z = Mathf.Lerp(-distance * 0.5f, distance * 0.5f, progress[i]);
            streaks[i].localPosition = local;

            // El largo se va a cero y el grosor solo adelgaza: llevando los dos a cero la
            // estela desaparece antes de tiempo y se pierde el efecto de alargarse.
            streaks[i].localScale = new Vector3(
                thickness[i] * (0.4f + 0.6f * borde),
                thickness[i] * (0.4f + 0.6f * borde),
                length[i] * borde);
        }
    }

    void Respawn(int i, bool keepProgress)
    {
        speed[i] = Random.Range(speedRange.x, speedRange.y);
        length[i] = Random.Range(lengthRange.x, lengthRange.y);
        thickness[i] = Random.Range(thicknessRange.x, thicknessRange.y);

        if (!keepProgress) progress[i] = 0f;

        streaks[i].localPosition = new Vector3(
            Random.Range(-spread.x, spread.x),
            Random.Range(-spread.y, spread.y),
            Mathf.Lerp(-distance * 0.5f, distance * 0.5f, progress[i]));

        streaks[i].localScale = new Vector3(thickness[i], thickness[i], length[i]);
    }
}
