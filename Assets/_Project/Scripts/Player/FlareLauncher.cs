using UnityEngine;

// Lanzabengalas. Botón derecho para apuntar en primera persona, izquierdo para
// disparar. No es un arma: es equipo de trabajo usado para desviar, no para pelear.
public class FlareLauncher : MonoBehaviour
{
    [SerializeField] PlayerController playerController;
    [SerializeField] PlayerCamera playerCamera;
    [SerializeField] Material flareMaterial;
    [SerializeField] HelmetVisorUI visorOverlay;

    [Header("Munición")]
    [SerializeField] int startingFlares = 3;
    [SerializeField] int maxFlares = 5;
    [SerializeField] float fireCooldown = 0.6f;

    float cooldownTimer;

    public int Flares { get; private set; }
    public int MaxFlares => maxFlares;
    public bool IsAiming { get; private set; }

    public event System.Action OnChanged;

    void Awake()
    {
        // Las bengalas vienen de la expedición, no de un campo local. Antes se regeneraban
        // al cargar cada mapa, así que gastarlas no costaba nada: eran gratis y por eso no
        // había ninguna decisión en usarlas. Ahora son un recurso de la ruta entera y las
        // que gastas hay que reponerlas en la tienda.
        Flares = Expedition.Flares;
    }

    public bool AddFlare()
    {
        if (Flares >= maxFlares) return false;
        Flares++;
        Expedition.Flares = Flares;
        OnChanged?.Invoke();
        return true;
    }

    void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        // Si el control está bloqueado (cinemática, forcejeo, muerte) no se apunta.
        bool canAim = playerController != null && playerController.enabled;
        bool wantsAim = canAim && Input.GetMouseButton(1);

        if (wantsAim != IsAiming) SetAiming(wantsAim);

        if (IsAiming && Input.GetMouseButtonDown(0) && cooldownTimer <= 0f && Flares > 0)
            Fire();
    }

    void SetAiming(bool aiming)
    {
        IsAiming = aiming;

        if (playerCamera != null) playerCamera.FirstPerson = aiming;
        if (playerController != null) playerController.Aiming = aiming;
        if (visorOverlay != null) visorOverlay.SetVisible(aiming);
    }

    void Fire()
    {
        Flares--;
        Expedition.Flares = Flares;
        cooldownTimer = fireCooldown;
        OnChanged?.Invoke();

        Vector3 origin = playerCamera.AimOrigin + playerCamera.AimRotation * Vector3.forward * 0.6f;
        var go = new GameObject("Bengala");
        go.transform.position = origin;

        go.AddComponent<FlareProjectile>()
          .Launch(playerCamera.AimRotation * Vector3.forward, flareMaterial);
    }

    void OnDisable()
    {
        // Si el componente se apaga en pleno apuntado hay que devolver la cámara,
        // si no queda trabada en primera persona sin forma de salir.
        if (IsAiming) SetAiming(false);
    }
}
