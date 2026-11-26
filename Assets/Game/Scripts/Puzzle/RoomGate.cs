using System.Collections.Generic;
using UnityEngine;

/// Reja, puerta o JAULA dentro de una sala (no es una puerta entre salas). Bloquea el paso hasta que se abre.
/// Dos usos:
///  - Reja de un GatedPuzzle: la abre el puzzle (deja "Open On Room Completed" desmarcado).
///  - Jaula de un objeto (ItemRoom): marca "Open On Room Completed" y se abre sola cuando la sala queda COMPLETADA.
/// Prefab: SpriteRenderer + uno o varios Collider2D solidos (no trigger, en la capa Walls) + este script.
/// IMPORTANTE: el Collectible NO debe ser hijo de la jaula (ponlo al lado, como hermano).
public class RoomGate : MonoBehaviour, IResettable
{
    [Tooltip("Colliders que bloquean el paso. Vacio = todos los Collider2D solidos (no trigger) del objeto y sus hijos.")]
    [SerializeField] Collider2D[] blockers;
    [SerializeField] SpriteRenderer sr;
    [Tooltip("Opcional: sprite cuando esta cerrada y cuando esta abierta. Si no hay sprite abierto, la reja se esconde al abrirse.")]
    [SerializeField] Sprite closedSprite;
    [SerializeField] Sprite openSprite;

    [Header("Sala completada")]
    [Tooltip("Marcado: se abre sola cuando la sala queda COMPLETADA (jaulas de objetos). Desmarcado: solo la abre un puzzle (GatedPuzzle).")]
    [SerializeField] bool openOnRoomCompleted = false;

    [Tooltip("Animaciones del perfil de Juice al abrirse y cerrarse.")]
    [SerializeField] string openShot = "DoorUnlock";
    [SerializeField] string closeShot = "DoorLock";
    [Tooltip("Tamano de la nube de polvo al abrirse o cerrarse (0 = sin polvo).")]
    [SerializeField] float dustScale = 1.3f;

    JuiceAnimator juice;
    Room room;

    public bool IsOpen { get; private set; }

    void Awake()
    {
        if (blockers == null || blockers.Length == 0)
        {
            var found = new List<Collider2D>();
            foreach (var c in GetComponentsInChildren<Collider2D>(true))
                if (!c.isTrigger) found.Add(c);
            blockers = found.ToArray();
        }
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

        juice = GetComponent<JuiceAnimator>();
        if (juice == null) juice = gameObject.AddComponent<JuiceAnimator>();
        juice.Configure(null, false, false);

        room = GetComponentInParent<Room>();
        if (room != null) room.RoomCompleted += OnRoomCompleted;

        SetOpen(false, false);
    }

    void OnDestroy()
    {
        if (room != null) room.RoomCompleted -= OnRoomCompleted;
    }

    void OnRoomCompleted()
    {
        if (openOnRoomCompleted) SetOpen(true, true);
    }

    /// Para llamarla desde un UnityEvent (por ejemplo, On Solved de un puzzle).
    public void Open() => SetOpen(true, true);
    public void Close() => SetOpen(false, true);

    /// Reinicio de la sala: una jaula de sala completada sigue abierta; si no, vuelve a cerrarse.
    /// Las rejas de un GatedPuzzle no se tocan aqui: las reinicia el propio puzzle.
    public void ResetState()
    {
        if (!openOnRoomCompleted) return;
        SetOpen(room != null && room.IsCompleted, false);
    }

    public void SetOpen(bool open, bool animate)
    {
        IsOpen = open;

        if (blockers != null)
            foreach (var b in blockers) if (b != null) b.enabled = !open;

        if (sr != null)
        {
            if (open)
            {
                if (openSprite != null) { sr.sprite = openSprite; sr.enabled = true; }
                else sr.enabled = false;
            }
            else
            {
                if (closedSprite != null) sr.sprite = closedSprite;
                sr.enabled = true;
            }
        }

        if (animate && isActiveAndEnabled)
        {
            if (juice != null) juice.Play(open ? openShot : closeShot);
            if (dustScale > 0f) Vfx.Play(VfxKind.Dust, transform.position, Vector2.up, dustScale);
            Sfx.Play(open ? SfxId.GateOpen : SfxId.GateClose, transform.position);
        }
    }
}
