using UnityEngine;

public enum DoorRole
{
    Forward = 0,                                // lleva a la siguiente sala (en la ÚLTIMA sala = ganar)
    [InspectorName("Back (YA NO SE USA)")]
    Back = 1,                                   // obsoleto: se ignora
    FinalExit = 2                               // puerta de victoria (solo cuenta en la última sala)
}

/// Puerta. Estructura recomendada:
///   ExitDoor (este script + BoxCollider2D con Is Trigger)
///     └─ Solid (BoxCollider2D normal, capa Walls)  -> arrástralo al campo Solid Collider
public class RoomDoor : MonoBehaviour, IResettable
{
    [SerializeField] DoorRole role = DoorRole.Forward;
    [Tooltip("Marcada: la puerta está abierta todo el tiempo, sin necesidad de recoger objetos.")]
    [SerializeField] bool alwaysOpen = false;
    [SerializeField] Collider2D solidCollider;
    [SerializeField] SpriteRenderer sr;

    [Header("Sprites (vacío = sprite del SpriteRenderer)")]
    [SerializeField] Sprite lockedSprite;
    [SerializeField] Sprite unlockedSprite;

    SpriteSwapper swapper;

    public DoorRole Role => role;
    public bool IsLocked { get; private set; } = true;

    void Awake()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        swapper = new SpriteSwapper(sr);
        if (solidCollider == null)
            Debug.LogWarning("RoomDoor '" + name + "' no tiene Solid Collider asignado: no bloqueará el paso aunque esté cerrada.", this);
        if (role == DoorRole.Back)
            Debug.LogWarning("RoomDoor '" + name + "' usa el rol Back, que ya no se usa. Cámbialo a Forward o FinalExit (o bórrala si es decorativa).", this);
        IsLocked = !alwaysOpen;
        Apply();
    }

    public void Unlock()
    {
        IsLocked = false;
        Apply();
    }

    public void Lock()
    {
        if (alwaysOpen) return;     // una puerta "siempre abierta" nunca se cierra
        IsLocked = true;
        Apply();
    }

    public void ResetState() => Lock();

    void Apply()
    {
        if (solidCollider != null) solidCollider.enabled = IsLocked;
        if (swapper != null) swapper.Show(IsLocked ? lockedSprite : unlockedSprite);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (IsLocked) return;
        if (!other.TryGetComponent(out PlayerController _)) return;
        if (GameManager.Instance != null) GameManager.Instance.OnDoorEntered(this);
    }
}
