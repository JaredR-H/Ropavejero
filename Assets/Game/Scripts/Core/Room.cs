using UnityEngine;

public enum RoomType { Spawn, Normal, Puzzle, FinalExit }

public class Room : MonoBehaviour
{
    [Header("Tipo")]
    [Tooltip("Solo una sala Spawn por mapa: es donde empieza el jugador.")]
    [SerializeField] RoomType type = RoomType.Normal;

    [Header("Puntos")]
    [SerializeField] Transform spawnPoint;
    [SerializeField] Transform cameraPoint;

    [Header("Tiempo")]
    [Tooltip("Desmarcalo en la sala Spawn si no quieres cronometro.")]
    [SerializeField] bool useTimeLimit = true;
    [SerializeField] float timeLimit = 60f;

    [SerializeField] Vector2 screenSize = new Vector2(16f, 9f);    // solo para el rectangulo amarillo si no hay camara ortografica

    Collectible[] collectibles;
    IResettable[] resettables;
    RoomDoor[] doors;
    readonly Room[] neighbors = new Room[4];

    public RoomType Type => type;
    public Vector2Int GridCell { get; private set; }

    public bool HasTimeLimit => useTimeLimit;
    public float TimeLimit => timeLimit;
    public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;
    public Vector3 CameraPosition => cameraPoint != null ? cameraPoint.position : transform.position;

    public RoomDoor[] Doors { get { EnsureInit(); return doors; } }
    public int CollectibleCount { get { EnsureInit(); return collectibles.Length; } }

    public int CollectedCount
    {
        get
        {
            EnsureInit();
            int n = 0;
            foreach (var c in collectibles) if (c.Collected) n++;
            return n;
        }
    }

    public bool AllCollected => CollectedCount >= CollectibleCount;

    void Awake() => EnsureInit();

    void EnsureInit()
    {
        if (collectibles != null) return;
        collectibles = GetComponentsInChildren<Collectible>(true);
        resettables = GetComponentsInChildren<IResettable>(true);
        doors = GetComponentsInChildren<RoomDoor>(true);
    }

    // ---------- Conexion con las salas vecinas (la hace MapLoader) ----------
    public void SetGridCell(Vector2Int cell) => GridCell = cell;

    public void Link(DoorSide side, Room other) => neighbors[(int)side] = other;

    public Room GetNeighbor(DoorSide side) => neighbors[(int)side];

    /// La puerta de paso de ese lado (no cuenta la puerta de victoria).
    public RoomDoor GetDoor(DoorSide side)
    {
        EnsureInit();
        foreach (var d in doors)
            if (d != null && d.Role == DoorRole.Forward && d.Side == side) return d;
        return null;
    }

    /// Muestra la puerta donde hay sala vecina y una pared donde no la hay.
    public void ConfigureDoors()
    {
        EnsureInit();
        foreach (var d in doors)
        {
            if (d == null || d.Role != DoorRole.Forward) continue;   // la puerta FinalExit no depende de vecinas
            d.SetConnected(neighbors[(int)d.Side] != null);
        }
    }

    public bool HasFinalExit()
    {
        EnsureInit();
        foreach (var d in doors)
            if (d != null && d.Role == DoorRole.FinalExit) return true;
        return false;
    }

    // ---------- Estado de la sala ----------
    /// Devuelve todo a su estado inicial (trampas, puzzle, objeto, puertas).
    /// Las puertas marcadas como Always Open se quedan abiertas.
    public void ResetRoom()
    {
        EnsureInit();
        foreach (var r in resettables) r.ResetState();
    }

    /// Abre las puertas de paso y de victoria de esta sala.
    public void UnlockDoors()
    {
        EnsureInit();
        foreach (var d in doors)
            if (d != null && d.Role != DoorRole.Back) d.Unlock();
    }

    /// Abre solo la(s) puerta(s) de victoria de esta sala.
    public void UnlockFinalExit()
    {
        EnsureInit();
        foreach (var d in doors)
            if (d != null && d.Role == DoorRole.FinalExit) d.Unlock();
    }

    /// El jugador sale con vida de la sala: los objetos recogidos quedan guardados y ya no reaparecen.
    public void BankCollectibles()
    {
        EnsureInit();
        foreach (var c in collectibles) c.Bank();
    }

    public void OnCollectibleTaken()
    {
        if (GameManager.Instance == null) return;
        if (AllCollected) GameManager.Instance.OnRoomItemsCollected(this);
        else GameManager.Instance.RefreshHud();
    }

    // El rectangulo amarillo es lo que vera la camara (usa el tamano de la Main Camera).
    void OnDrawGizmos()
    {
        Vector2 size = screenSize;
        var cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float h = cam.orthographicSize * 2f;
            size = new Vector2(h * (16f / 9f), h);
        }

        Gizmos.color = new Color(1f, 1f, 0f, 0.8f);
        Gizmos.DrawWireCube(CameraPosition, new Vector3(size.x, size.y, 0f));

        if (type == RoomType.Spawn && spawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.3f);
        }
    }
}
