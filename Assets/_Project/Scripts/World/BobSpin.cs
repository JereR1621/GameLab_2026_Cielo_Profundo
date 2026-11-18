using UnityEngine;

// Flotar y girar despacio sobre un punto fijo. Lo usan los objetos recogibles y los
// signos de interacción de la nave, así que los dos se mueven exactamente igual.
public class BobSpin : MonoBehaviour
{
    [SerializeField] float spinSpeed = 30f;
    [SerializeField] float bobHeight = 0.12f;
    [SerializeField] float bobSpeed = 1.4f;
    [SerializeField] float phase;

    Vector3 restPosition;

    void Awake()
    {
        restPosition = transform.position;
    }

    // Para cuando la posición se fija después de crear el objeto.
    public void SetRestPosition(Vector3 position)
    {
        restPosition = position;
    }

    public void Configure(float spin, float height, float speed, float phaseOffset = 0f)
    {
        spinSpeed = spin;
        bobHeight = height;
        bobSpeed = speed;
        phase = phaseOffset;
    }

    void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        float y = restPosition.y + Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
        transform.position = new Vector3(restPosition.x, y, restPosition.z);
    }
}
