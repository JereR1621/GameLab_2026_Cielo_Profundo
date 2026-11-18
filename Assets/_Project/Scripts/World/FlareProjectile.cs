using UnityEngine;

public class FlareProjectile : MonoBehaviour
{
    [SerializeField] float speed = 26f;
    [SerializeField] float gravity = -7f;
    [SerializeField] float maxLifetime = 6f;
    [SerializeField] float hitRadius = 0.35f;

    Vector3 velocity;
    float age;
    Material burnMaterial;

    public void Launch(Vector3 direction, Material material)
    {
        velocity = direction.normalized * speed;
        burnMaterial = material;

        var mesh = LowPolyMeshFactory.CreateCrystal(5, 0.35f, 0.09f, Random.Range(0, 9999));
        var visual = new GameObject("Cuerpo");
        visual.transform.SetParent(transform, false);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        visual.AddComponent<MeshRenderer>().sharedMaterial = material;

        // En vuelo también alumbra: se ve la estela pasar por encima del terreno.
        var trail = new GameObject("Luz");
        trail.transform.SetParent(transform, false);
        var light = trail.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.32f);
        light.range = 22f;
        light.intensity = 14f;
    }

    void Update()
    {
        age += Time.deltaTime;
        velocity.y += gravity * Time.deltaTime;

        Vector3 step = velocity * Time.deltaTime;

        // SphereCast y no Raycast: a esta velocidad un rayo fino puede colarse entre
        // dos triángulos del terreno y la bengala seguiría de largo hacia el vacío.
        if (Physics.SphereCast(transform.position, hitRadius, step.normalized,
                               out var hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            Detonate(hit.point + hit.normal * 0.3f);
            return;
        }

        transform.position += step;
        transform.Rotate(Vector3.right, 420f * Time.deltaTime, Space.Self);

        if (age >= maxLifetime) Detonate(transform.position);
    }

    void Detonate(Vector3 position)
    {
        var burstGO = new GameObject("FogonazoBengala");
        burstGO.transform.position = position;

        var flame = new GameObject("Llama");
        flame.transform.SetParent(burstGO.transform, false);
        flame.AddComponent<MeshFilter>().sharedMesh =
            LowPolyMeshFactory.CreateCrystal(6, 1.1f, 0.24f, Random.Range(0, 9999));
        flame.AddComponent<MeshRenderer>().sharedMaterial = burnMaterial;
        flame.transform.localPosition = new Vector3(0f, 0.45f, 0f);

        burstGO.AddComponent<FlareBurst>().Detonate();
        Destroy(gameObject);
    }
}
