using UnityEngine;

/// Una habitación = una pantalla completa. Va en el objeto raíz "Room_X".
/// Todo el contenido (arte, spawn, trampas, puzzle, objeto, puertas) debe ser HIJO de este objeto.
/// Las puertas (RoomDoor) se detectan solas: no hay que asignarlas a mano.
public class Room : MonoBehaviour
{
    [Header("Puntos")]
    [SerializeField] Transform spawnPoint;            // donde apareces al entrar y al reaparecer
    [SerializeField] Transform cameraPoint;           // centro de lo que verá la cámara

    [Header("Tiempo")]
    [Tooltip("Desmárcalo en las salas de introducción si no quieres cronómetro.")]
    [SerializeField] bool useTimeLimit = true;
    [SerializeField] float timeLimit = 60f;

    [SerializeField] Vector2 screenSize = new Vector2(16f, 9f);    // solo si no hay cámara ortográfica en la escena

    Collectible[] collectibles;
    IResettable[] resettables;
    RoomDoor[] doors;

    public bool HasTimeLimit => useTimeLimit;
    public float TimeLimit => timeLimit;
    public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;
    public Vector3 CameraPosition => cameraPoint != null ? cameraPoint.position : transform.position;

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

    /// Devuelve todo a su estado inicial (trampas, puzzle, objeto, puertas).
    /// Las puertas marcadas como Always Open se quedan abiertas.
    public void ResetRoom()
    {
        EnsureInit();
        foreach (var r in resettables) r.ResetState();
    }

    /// Abre las puertas de avance / victoria de esta sala.
    public void UnlockDoors()
    {
        EnsureInit();
        foreach (var d in doors)
            if (d.Role != DoorRole.Back) d.Unlock();
    }

    public void OnCollectibleTaken()
    {
        if (GameManager.Instance == null) return;
        if (AllCollected) GameManager.Instance.OnRoomItemsCollected(this);
        else GameManager.Instance.RefreshHud();
    }

    // El rectángulo amarillo es EXACTAMENTE lo que verá la cámara (usa el tamaño de la Main Camera).
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

        if (spawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.3f);
        }
    }
}