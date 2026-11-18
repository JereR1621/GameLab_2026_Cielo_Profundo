using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class RespawnOnFall : MonoBehaviour
{
    [SerializeField] float fallLimitY = -40f;

    Vector3 spawnPoint;
    CharacterController controller;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        spawnPoint = transform.position;
    }

    public void SetSpawnPoint(Vector3 point)
    {
        spawnPoint = point;
    }

    void Update()
    {
        if (transform.position.y >= fallLimitY) return;

        controller.enabled = false;
        transform.position = spawnPoint;
        controller.enabled = true;

        // Caerse cuesta un corazón. Antes era gratis: te devolvía al punto de partida y
        // ya está, así que tirarse por un borde era un atajo para volver al aterrizaje
        // más rápido que caminando. Ahora se paga, y con ello viene la invulnerabilidad
        // de siempre, que evita que reaparezcas encima de una araña y mueras dos veces.
        //
        // El resguardo de invulnerabilidad importa además por otro motivo: sin él, dos
        // cuadros seguidos por debajo del límite descontarían dos corazones.
        var salud = PlayerHealth.Instance;
        if (salud != null && !salud.IsInvulnerable) salud.LoseHeart();
    }
}
