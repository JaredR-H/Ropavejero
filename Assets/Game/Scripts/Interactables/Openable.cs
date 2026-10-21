using UnityEngine;

/// Reja / compuerta de puzzle. Bloquea el paso mientras está cerrada.
/// Se abre desde un HitSwitch o un BreakableObject (eventos del inspector).
/// Si 'Open Sprite' está vacío, la reja se OCULTA al abrirse.
public class Openable : MonoBehaviour, IResettable
{
    [SerializeField] Collider2D blocker;               // collider sólido (si está vacío usa el del mismo objeto)
    [SerializeField] SpriteRenderer sr;
    [SerializeField] bool startsOpen = false;

    [Header("Sprites (Closed vacío = sprite del SpriteRenderer)")]
    [SerializeField] Sprite closedSprite;
    [SerializeField] Sprite openSprite;

    SpriteSwapper swapper;

    public bool IsOpen { get; private set; }

    void Awake()
    {
        if (blocker == null) blocker = GetComponent<Collider2D>();
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        swapper = new SpriteSwapper(sr);
        ResetState();
    }

    public void Open() => Set(true);
    public void Close() => Set(false);
    public void Toggle() => Set(!IsOpen);
    public void ResetState() => Set(startsOpen);

    void Set(bool open)
    {
        IsOpen = open;
        if (blocker != null) blocker.enabled = !open;
        if (sr == null || swapper == null) return;

        if (open && openSprite == null)
        {
            sr.enabled = false;            // sin sprite de abierto: la reja desaparece
            return;
        }

        sr.enabled = true;
        swapper.Show(open ? openSprite : closedSprite);
    }
}
