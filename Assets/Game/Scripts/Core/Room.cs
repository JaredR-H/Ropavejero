using System.Collections.Generic;
using UnityEngine;

/// Solo Spawn cambia la logica; los demas son etiquetas para ordenar los mapas (FinalExit = sala del jefe).
/// Los nuevos van AL FINAL para no cambiar los valores ya guardados en los prefabs.
public enum RoomType { Spawn, Normal, Puzzle, FinalExit, Trap, Item, Enemy, Safe, Shop }

/// Cuando aparece el arma del jugador (y cuando puede atacar) en esta sala.
public enum WeaponRule
{
    [InspectorName("Auto (solo con enemigos sin derrotar)")] Auto = 0,
    [InspectorName("Hasta completar la sala (puzzles que se golpean)")] UntilCompleted = 1,
    [InspectorName("Siempre")] Always = 2,
    [InspectorName("Nunca")] Never = 3
}

/// Una habitacion = una pantalla completa. Va en el objeto raiz de CADA prefab de sala.
/// El centro del objeto raiz debe ser el centro de la sala.
/// Todo el contenido (arte, trampas, puzzle, objeto, puertas) debe ser HIJO de este objeto.
/// Las puertas (RoomDoor), los objetos (Collectible), los puzzles, las oleadas y los conjuntos de trampas se detectan solos.
/// Los vecinos los asigna MapLoader al cargar el mapa, segun la posicion de cada sala.
///
/// COMPLETADA: una sala con objetivos (oleadas de enemigos y/o puzzle) queda completada cuando se cumplen todos.
/// Entonces: se apagan sus trampas (y ya no hacen dano), se abren las puertas "Always Open" y desaparece la cuenta regresiva.
/// Una sala completada se queda asi aunque salgas y vuelvas a entrar.
public class Room : MonoBehaviour
{
    [Header("Tipo")]
    [Tooltip("Solo una sala Spawn por mapa: es donde empieza el jugador.")]
    [SerializeField] RoomType type = RoomType.Normal;

    [Header("Conexiones")]
    [Tooltip("Lados por los que esta sala NO se conecta aunque tenga una sala pegada (ahi se muestra pared). " +
             "Basta con marcarlo en una de las dos salas. Se configura en la sala dentro del prefab del MAPA.")]
    [SerializeField] DoorSide[] sealedSides;

    [Header("Jefe / reaparicion")]
    [Tooltip("Marcado (sala del jefe): al morir la sala NO se reinicia (el jefe conserva su vida y los enemigos siguen donde estan).")]
    [SerializeField] bool keepStateOnDeath = false;
    [Tooltip("Puntos donde puede reaparecer el jugador si 'Keep State On Death' esta marcado: se usa el mas lejano al jefe. " +
             "Vacio = las cuatro esquinas de la sala.")]
    [SerializeField] Transform[] respawnPoints;
    [Tooltip("Segundos de invulnerabilidad al reaparecer en esta sala (con parpadeo blanco).")]
    [SerializeField, Min(0f)] float respawnInvulnerability = 0f;
    [Tooltip("Marcado (sala del jefe): las puertas que llevan a esta sala siguen cerradas hasta tener TODOS los objetos.")]
    [SerializeField] bool requiresAllItems = false;

    [Header("Arma del jugador")]
    [Tooltip("Auto: el arma solo aparece si la sala tiene oleadas de enemigos y no esta completada.\n" +
             "Hasta completar: aparece hasta resolver la sala (para puzzles con botones que se golpean o vasijas).\n" +
             "Siempre: por ejemplo, la sala del jefe. Nunca: no se puede atacar aqui.")]
    [SerializeField] WeaponRule weaponRule = WeaponRule.Auto;

    [Header("Puntos")]
    [SerializeField] Transform spawnPoint;            // solo en la sala Spawn: donde aparece el jugador
    [SerializeField] Transform cameraPoint;           // centro de lo que vera la camara (si esta vacio, el centro de la sala)

    [Header("Tiempo")]
    [Tooltip("Desmarcalo en la sala Spawn si no quieres cronometro. Una sala completada nunca tiene cronometro.")]
    [SerializeField] bool useTimeLimit = true;
    [SerializeField] float timeLimit = 60f;
    [Tooltip("Dificultad progresiva: si es mayor que 0, el limite baja hasta este valor en las salas mas lejanas del inicio del mapa.")]
    [SerializeField, Min(0f)] float timeLimitAtMaxDepth = 0f;

    [Tooltip("Tamano de la sala en unidades (lo que debe ver la camara). La camara nunca muestra nada fuera de este rectangulo.")]
    [SerializeField] Vector2 screenSize = new Vector2(16f, 9f);

    Collectible[] collectibles;
    IResettable[] resettables;
    RoomDoor[] doors;
    IRoomObjective[] objectives;
    TrapSet[] trapSets;
    bool hasEncounter;
    readonly Room[] neighbors = new Room[4];

    public RoomType Type => type;
    public bool IsBossRoom => type == RoomType.FinalExit;
    public bool KeepStateOnDeath => keepStateOnDeath;
    public float RespawnInvulnerability => respawnInvulnerability;
    public bool RequiresAllItems => requiresAllItems;

    /// El punto de reaparicion mas lejano a 'threat' (por ejemplo, el jefe).
    public Vector3 FarthestRespawnPoint(Vector2 threat)
    {
        var candidates = new List<Vector3>();
        if (respawnPoints != null)
            foreach (var p in respawnPoints)
                if (p != null) candidates.Add(p.position);

        if (candidates.Count == 0)
        {
            Vector2 c = CameraPosition;
            Vector2 h = ViewSize * 0.5f - new Vector2(2f, 1.5f);      // las esquinas, lejos de las paredes
            candidates.Add(c + new Vector2(-h.x, -h.y));
            candidates.Add(c + new Vector2(h.x, -h.y));
            candidates.Add(c + new Vector2(-h.x, h.y));
            candidates.Add(c + new Vector2(h.x, h.y));
        }

        Vector3 best = candidates[0];
        float bestDist = -1f;
        foreach (var p in candidates)
        {
            float d = ((Vector2)p - threat).sqrMagnitude;
            if (d > bestDist) { bestDist = d; best = p; }
        }
        return best;
    }
    public Vector2Int GridCell { get; private set; }

    /// Distancia (en salas) desde la sala de inicio del mapa.
    public int Depth { get; private set; }
    /// 0 = lo mas facil (cerca del inicio), 1 = lo mas dificil (lo mas lejos del inicio).
    public float DifficultyT { get; private set; }
    public bool IsCompleted { get; private set; }

    /// Se dispara una sola vez cuando la sala queda completada.
    public event System.Action RoomCompleted;

    // Una sala completada no tiene cuenta regresiva
    public bool HasTimeLimit => useTimeLimit && !IsCompleted;
    public float TimeLimit => timeLimitAtMaxDepth > 0f ? Mathf.Lerp(timeLimit, timeLimitAtMaxDepth, DifficultyT) : timeLimit;
    public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;
    public Vector3 CameraPosition => cameraPoint != null ? cameraPoint.position : transform.position;
    /// Tamano de la sala en unidades del mundo (para que la camara no se salga).
    public Vector2 ViewSize => screenSize;

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
        objectives = GetComponentsInChildren<IRoomObjective>(true);
        trapSets = GetComponentsInChildren<TrapSet>(true);
        hasEncounter = GetComponentsInChildren<RoomEncounter>(true).Length > 0;
    }

    /// true si en esta sala, ahora mismo, el jugador lleva el arma y puede atacar.
    public bool WeaponAllowed
    {
        get
        {
            EnsureInit();
            switch (weaponRule)
            {
                case WeaponRule.Always: return true;
                case WeaponRule.Never: return false;
                case WeaponRule.UntilCompleted: return !IsCompleted;
                default: return hasEncounter && !IsCompleted;
            }
        }
    }

    // ---------- Conexion con las salas vecinas (la hace MapLoader) ----------
    public void SetGridCell(Vector2Int cell) => GridCell = cell;

    public void Link(DoorSide side, Room other) => neighbors[(int)side] = other;

    public Room GetNeighbor(DoorSide side) => neighbors[(int)side];

    /// true si ese lado esta bloqueado a proposito (pared aunque haya sala vecina).
    public bool IsSealed(DoorSide side)
    {
        if (sealedSides == null) return false;
        foreach (var s in sealedSides) if (s == side) return true;
        return false;
    }

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

    // ---------- Dificultad progresiva (la asigna MapLoader) ----------
    public void SetDepth(int depth, int maxDepth)
    {
        Depth = depth;
        DifficultyT = maxDepth > 0 ? Mathf.Clamp01((float)depth / maxDepth) : 0f;
    }

    /// Aplica la dificultad de la sala a lo que escala con ella (conjuntos de trampas).
    public void ApplyDifficulty()
    {
        EnsureInit();
        foreach (var t in trapSets)
            if (t != null) t.ApplyDifficulty(DifficultyT);
    }

    // ---------- Objetivos y sala completada ----------
    /// true si la sala no tiene objetivos o ya se cumplieron.
    public bool EncounterCompleted
    {
        get { EnsureInit(); return IsCompleted || objectives.Length == 0; }
    }

    /// El jugador entro (o reaparecio) en la sala: si tiene objetivos sin cumplir, las puertas se cierran y empiezan.
    public void NotifyEntered()
    {
        EnsureInit();
        if (IsCompleted || objectives.Length == 0) return;

        SetEncounterDoorsLocked(true);

        foreach (var o in objectives)
        {
            if (IsCompleted) break;
            o.Begin();
        }
    }

    /// Lo llama cada objetivo (puzzle, oleadas) al cumplirse. Cuando estan todos, la sala queda completada.
    public void ObjectiveCompleted(IRoomObjective source)
    {
        EnsureInit();
        foreach (var o in objectives)
            if (!o.IsDone) return;

        MarkCompleted();
    }

    /// Sala COMPLETADA: trampas fuera, puertas abiertas, sin cuenta regresiva.
    /// Si hay una RoomCompleteCinematic en la escena, lo hace paso a paso con la mini cinematica; si no, al instante.
    /// Desde este momento la sala ya cuenta como completada: sin cronometro y sin dano.
    public void MarkCompleted()
    {
        if (IsCompleted) return;
        IsCompleted = true;
        EnsureInit();

        var gm = GameManager.Instance;
        if (gm != null) gm.OnRoomCompleted(this);

        var cinematic = RoomCompleteCinematic.Instance;
        if (cinematic != null && gm != null && gm.CurrentRoom == this && cinematic.TryPlay(this)) return;

        // Sin cinematica: todo al instante
        DeactivateTraps();
        SetEncounterDoorsLocked(false);     // las que estaban cerradas de origen (Always Open desmarcado) siguen igual
        RaiseCompleted();
    }

    // ---------- Piezas de "sala completada" (las usa la cinematica) ----------
    public TrapSet[] TrapSets { get { EnsureInit(); return trapSets; } }

    /// Las puertas de paso que se van a abrir al completar la sala (estan visibles y cerradas por el encuentro).
    public List<RoomDoor> DoorsThatWillOpen()
    {
        EnsureInit();
        var list = new List<RoomDoor>();
        foreach (var d in doors)
            if (d != null && d.Role == DoorRole.Forward && d.gameObject.activeInHierarchy && d.IsLocked) list.Add(d);
        return list;
    }

    public void DeactivateTraps()
    {
        EnsureInit();
        foreach (var t in trapSets)
            if (t != null) t.Deactivate();
    }

    /// Avisa de que la sala quedo completada (HUD sin tiempo, jaulas que se abren solas...).
    public void RaiseCompleted()
    {
        EnsureInit();
        SetEncounterDoorsLocked(false);     // por si alguna quedo sin abrir
        if (GameManager.Instance != null) GameManager.Instance.RefreshHud();
        RoomCompleted?.Invoke();
    }

    /// Cierra o abre TODAS las puertas de paso por los objetivos de la sala. Al abrir solo se abren las que
    /// no estaban cerradas de origen (Always Open); las otras conservan su estado.
    public void SetEncounterDoorsLocked(bool locked)
    {
        EnsureInit();
        foreach (var d in doors)
            if (d != null && d.Role == DoorRole.Forward) d.SetEncounterLocked(locked);
    }

    // ---------- Estado de la sala ----------
    /// Devuelve todo a su estado inicial (trampas, puzzle, objeto, puertas).
    /// Una sala completada conserva su estado de completada: sus trampas siguen apagadas y su puzzle resuelto.
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
