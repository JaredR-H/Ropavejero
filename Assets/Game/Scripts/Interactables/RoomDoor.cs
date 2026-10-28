using UnityEngine;

public enum DoorSide { Up = 0, Down = 1, Left = 2, Right = 3 }

public static class DoorSideExtensions
{
    public static DoorSide Opposite(this DoorSide side)
    {
        switch (side)
        {
            case DoorSide.Up: return DoorSide.Down;
            case DoorSide.Down: return DoorSide.Up;
            case DoorSide.Left: return DoorSide.Right;
            default: return DoorSide.Left;
        }
    }

    public static Vector2 ToWorldDirection(this DoorSide side)
    {
        switch (side)
        {
            case DoorSide.Up: return Vector2.up;
            case DoorSide.Down: return Vector2.down;
            case DoorSide.Left: return Vector2.left;
            default: return Vector2.right;
        }
    }

    public static Vector2Int ToGridOffset(this DoorSide side)
    {
        switch (side)
        {
            case DoorSide.Up: return new Vector2Int(0, 1);
            case DoorSide.Down: return new Vector2Int(0, -1);
            case DoorSide.Left: return new Vector2Int(-1, 0);
            default: return new Vector2Int(1, 0);
        }
    }
}

public enum DoorRole
{
    Forward = 0,                                // puerta de paso a la sala vecina de su lado
    [InspectorName("Back (YA NO SE USA)")]
    Back = 1,                                   // obsoleto: se ignora
    FinalExit = 2                               // puerta de victoria
}


public class RoomDoor : MonoBehaviour, IResettable
{
    [SerializeField] DoorRole role = DoorRole.Forward;
    [Tooltip("Marcada: la puerta esta abierta todo el tiempo. Desmarcada: se abre al recoger el objeto de la sala (o con Unlock).")]
    [SerializeField] bool alwaysOpen = false;
    [SerializeField] Collider2D solidCollider;
    [SerializeField] SpriteRenderer sr;

    [Header("Lado")]
    [Tooltip("En que lado de la sala esta esta puerta (Up, Down, Left, Right).")]
    [SerializeField] DoorSide side = DoorSide.Up;
    [Tooltip("Pared que se muestra si por este lado no hay sala vecina. NO puede ser hijo de la puerta.")]
    [SerializeField] GameObject blockedVisual;

    [Header("Sprites (vacio = sprite del SpriteRenderer)")]
    [SerializeField] Sprite lockedSprite;
    [SerializeField] Sprite unlockedSprite;

    SpriteSwapper swapper;

    public DoorRole Role => role;
    public bool AlwaysOpen => alwaysOpen;
    public DoorSide Side => side;
    public bool IsLocked { get; private set; } = true;

    void Awake()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        swapper = new SpriteSwapper(sr);
        if (solidCollider == null)
            Debug.LogWarning("RoomDoor '" + name + "' no tiene Solid Collider asignado: no bloqueara el paso aunque este cerrada.", this);
        if (role == DoorRole.Back)
            Debug.LogWarning("RoomDoor '" + name + "' usa el rol Back, que ya no se usa. Cambialo a Forward o FinalExit (o borrala si es decorativa).", this);
        IsLocked = !alwaysOpen;
        Apply();
    }

    /// Puerta si hay sala vecina, pared si no.
    public void SetConnected(bool connected)
    {
        if (!connected && blockedVisual == null)
            Debug.LogWarning("RoomDoor '" + name + "' no tiene Blocked Visual: donde no haya sala vecina se vera un hueco en la pared.", this);

        gameObject.SetActive(connected);
        if (blockedVisual != null) blockedVisual.SetActive(!connected);
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
