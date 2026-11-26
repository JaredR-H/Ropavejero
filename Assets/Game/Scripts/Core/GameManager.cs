using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Playing, Dead, Transition, GameOver, Win }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Nivel")]
    [SerializeField] MapLoader mapLoader;

    [Header("Referencias")]
    [SerializeField] PlayerController player;
    [SerializeField] Camera mainCamera;
    [SerializeField] UIManager ui;

    [Header("Audio y fin de partida")]
    [Tooltip("Opcional: secuencia del saco cuando se acaba el tiempo (TimeUpSequence). Vacio = fundido a negro y Game Over.")]
    [SerializeField] TimeUpSequence timeUpSequence;
    [Tooltip("Fundido entre la musica de las salas y la del jefe.")]
    [SerializeField] float roomMusicFade = 1.5f;
    [Tooltip("Al acabarse el tiempo: segundos que tarda en apagarse la musica (junto con el fundido a negro).")]
    [SerializeField] float timeUpMusicFade = 0.8f;
    [SerializeField] IrisTransition iris;          // opcional: solo se usa al empezar la partida
    [Tooltip("Opcional: dirige la camara (shake, zoom), el hitstop, la camara lenta y el congelado al morir. Sin el, el juego funciona sin esos efectos.")]
    [SerializeField] GameFeel gameFeel;
    [Tooltip("Opcional: fundido a negro lento al reiniciar. Si esta vacio se usa el ScreenFader de la escena (si hay).")]
    [SerializeField] ScreenFader fader;

    [Header("Reglas")]
    [Tooltip("Si esta activo, al morir en una trampa el tiempo de la sala vuelve a empezar.")]
    [SerializeField] bool resetTimerOnDeath = true;
    [SerializeField] float respawnDelay = 0.6f;
    [Tooltip("Segundos de invulnerabilidad (parpadeo blanco) al reaparecer tras morir. " +
             "Si la sala tiene su propio 'Respawn Invulnerability' mayor que 0 (p. ej. la del jefe), se usa el de la sala.")]
    [SerializeField, Min(0f)] float respawnInvulnerability = 1.5f;
    [Tooltip("Tras entrar a una sala se ignoran las puertas este tiempo, para no rebotar de vuelta.")]
    [SerializeField] float doorCooldown = 0.4f;

    [Header("Deslizamiento entre salas")]
    [Tooltip("Segundos que dura el movimiento de camara (el juego esta congelado mientras tanto).")]
    [SerializeField] float slideDuration = 0.6f;
    [Tooltip("Cuanto avanza el jugador DENTRO de la sala nueva, medido desde el centro de su puerta de llegada. Debe quedar fuera del trigger de esa puerta.")]
    [SerializeField] float walkInDistance = 1.6f;

    [Header("Jefe")]
    [Tooltip("Veces que se puede volver a la sala segura (checkpoint) si se acaba el tiempo con el jefe. Despues, a empezar de cero.")]
    [SerializeField, Min(0)] int bossRetries = 2;
    [Tooltip("Segundos entre avisos de 'te faltan objetos' al chocar con la puerta del jefe.")]
    [SerializeField] float gateHintCooldown = 2f;

    public enum DebugStart { Off, Boss, Safe, Item, Puzzle, Trap, Enemy, Shop, ByName }

    [Header("DEBUG: empezar en otra sala (solo pruebas)")]
    [Tooltip("Off = partida normal desde el Spawn. Otro valor = empiezas en la primera sala de ese tipo del mapa. " +
             "ByName = la sala cuyo GameObject se llama como 'Debug Room Name'.")]
    [SerializeField] DebugStart debugStartRoom = DebugStart.Off;
    [Tooltip("Para ByName: nombre exacto del GameObject de la sala en el mapa (p. ej. 'ROOM 12').")]
    [SerializeField] string debugRoomName = "";
    [Tooltip("Da todos los recuerdos al empezar (para que la puerta del jefe este abierta).")]
    [SerializeField] bool debugGiveAllItems = false;
    [Tooltip("Marcado: el debug solo funciona en el Editor; en el juego exportado (build) siempre se empieza normal.")]
    [SerializeField] bool debugOnlyInEditor = true;

    [Header("Inicio de partida (iris)")]
    [SerializeField] float irisHoldTime = 0.15f;   // tiempo con la pantalla cerrada
    [SerializeField] float irisOpenTime = 0.5f;

    PlayerHealth health;
    List<Room> allRooms = new List<Room>();
    Room currentRoom;
    Vector3 entryPosition;          // donde reapareces al morir en la sala actual
    int totalItems;
    float timeLeft;
    float totalTime;
    float ignoreDoorsUntil;
    Room lastSafeRoom;            // checkpoint para el jefe
    int bossRetriesLeft;
    bool mustRestartGame;         // sin reintentos: el boton de Game Over reinicia toda la partida
    float nextGateHint;

    public GameState State { get; private set; } = GameState.Transition;
    public bool IsPlaying => State == GameState.Playing;
    public int Deaths { get; private set; }
    public Room CurrentRoom => currentRoom;

    ScreenFader Fader => fader != null ? fader : ScreenFader.Instance;

    public int ItemsCollected
    {
        get
        {
            int n = 0;
            foreach (var r in allRooms) n += r.CollectedCount;
            return n;
        }
    }

    // Si el mapa no tiene ningun objeto, la salida esta abierta desde el principio.
    bool AllItemsCollected => ItemsCollected >= totalItems;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        health = player.GetComponent<PlayerHealth>();
        bossRetriesLeft = bossRetries;

        if (mapLoader == null)
        {
            Debug.LogError("GameManager: falta asignar el Map Loader.", this);
            return;
        }

        allRooms = mapLoader.Load();
        Room first = mapLoader.SpawnRoom;
        if (first == null)
        {
            Debug.LogError("GameManager: el mapa no tiene sala Spawn. Revisa la Console.", this);
            return;
        }

        // Las salas se crean activas; en juego solo vive la actual.
        foreach (var r in allRooms)
        {
            totalItems += r.CollectibleCount;
            r.gameObject.SetActive(false);
        }

        ui.HidePanels();

        Vector3 firstEntry = first.SpawnPosition;
        if (DebugEnabled)
        {
            if (debugGiveAllItems) DebugGiveAllItems();
            Room debugRoom = FindDebugRoom();
            if (debugRoom != null)
            {
                first = debugRoom;
                firstEntry = DebugEntryPosition(debugRoom);
                // Checkpoint del jefe: la primera sala segura del mapa
                if (lastSafeRoom == null) lastSafeRoom = allRooms.Find(r => r != null && r.Type == RoomType.Safe);
                Debug.Log("GameManager DEBUG: empezando en '" + debugRoom.name + "' (" + debugRoom.Type + ").", debugRoom);
            }
            else if (debugStartRoom != DebugStart.Off)
            {
                Debug.LogWarning("GameManager DEBUG: no encontre la sala pedida (" + debugStartRoom +
                                 (debugStartRoom == DebugStart.ByName ? " '" + debugRoomName + "'" : "") + "). Empiezo en el Spawn.", this);
            }
        }

        EnterRoom(first, firstEntry, false);
        StartCoroutine(IntroRoutine());
    }

    // ---------- Debug ----------
    bool DebugEnabled => !debugOnlyInEditor || Application.isEditor;

    Room FindDebugRoom()
    {
        if (debugStartRoom == DebugStart.Off) return null;
        foreach (var r in allRooms)
        {
            if (r == null) continue;
            switch (debugStartRoom)
            {
                case DebugStart.ByName:
                    if (!string.IsNullOrEmpty(debugRoomName) && r.name.Trim() == debugRoomName.Trim()) return r;
                    break;
                case DebugStart.Boss: if (r.IsBossRoom || r.GetComponentInChildren<BossController>(true) != null) return r; break;
                case DebugStart.Safe: if (r.Type == RoomType.Safe) return r; break;
                case DebugStart.Item: if (r.Type == RoomType.Item) return r; break;
                case DebugStart.Puzzle: if (r.Type == RoomType.Puzzle) return r; break;
                case DebugStart.Trap: if (r.Type == RoomType.Trap) return r; break;
                case DebugStart.Enemy: if (r.Type == RoomType.Enemy) return r; break;
                case DebugStart.Shop: if (r.Type == RoomType.Shop) return r; break;
            }
        }
        return null;
    }

    // Aparece como si hubiera entrado por una puerta (preferiblemente la que viene de la sala segura)
    Vector3 DebugEntryPosition(Room room)
    {
        if (room.Type == RoomType.Spawn) return room.SpawnPosition;
        RoomDoor best = null;
        foreach (var d in room.Doors)
        {
            if (d == null) continue;
            Room other = room.GetNeighbor(d.Side);
            if (other == null) continue;
            if (best == null || other.Type == RoomType.Safe) best = d;
            if (other.Type == RoomType.Safe) break;
        }
        if (best == null) return room.SpawnPosition;

        Vector2 inward = -best.Side.ToWorldDirection();
        Vector3 p = best.transform.position + (Vector3)(inward * walkInDistance);
        p.z = player.transform.position.z;
        return p;
    }

    void DebugGiveAllItems()
    {
        foreach (var r in allRooms)
        {
            if (r == null) continue;
            foreach (var c in r.GetComponentsInChildren<Collectible>(true)) c.DebugCollect();
        }
        Debug.Log("GameManager DEBUG: recuerdos " + ItemsCollected + "/" + totalItems + ".", this);
    }

    void Update()
    {
        if (State != GameState.Playing || currentRoom == null) return;

        totalTime += Time.deltaTime;
        if (!currentRoom.HasTimeLimit) return;

        timeLeft -= Time.deltaTime;
        ui.SetTime(timeLeft);

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            ui.SetTime(0f);
            OnTimeUp();
        }
    }

    // ---------- Camara ----------
    // Si hay GameFeel, el dirige la camara (shake y zoom se suman encima de esta posicion base).
    Vector3 GetCameraBase() => gameFeel != null ? gameFeel.CameraBase : mainCamera.transform.position;

    void SetCameraBase(Vector3 position)
    {
        if (gameFeel != null) gameFeel.CameraBase = position;
        else mainCamera.transform.position = position;
    }

    // ---------- Salas ----------
    /// entry = donde aparece el jugador (y donde reaparece al morir en esta sala).
    /// startPlaying = false deja al jugador congelado (estado Transition) hasta que termine la intro.
    void EnterRoom(Room room, Vector3 entry, bool startPlaying = true)
    {
        currentRoom = room;
        UpdateRoomMusic(room);
        entryPosition = entry;
        if (room.Type == RoomType.Safe) lastSafeRoom = room;

        room.gameObject.SetActive(true);
        room.ResetRoom();
        ApplyProgress(room);

        Vector3 camPos = room.CameraPosition;
        SetCameraBase(new Vector3(camPos.x, camPos.y, -10f));

        health.Revive(entry);
        timeLeft = room.TimeLimit;

        if (startPlaying) BeginPlaying();
        else
        {
            State = GameState.Transition;
            player.InputEnabled = false;
        }
        RefreshHud();
    }

    /// Lo que ya se logro en la partida se refleja en la sala al (re)entrar o reiniciarla.
    void ApplyProgress(Room room, bool animateGates = false)
    {
        // Puertas que llevan a una sala que pide todos los objetos (el jefe): cerradas hasta tenerlos
        foreach (var d in room.Doors)
        {
            if (d == null || d.Role != DoorRole.Forward) continue;
            Room other = room.GetNeighbor(d.Side);
            d.SetGateLocked(other != null && other.RequiresAllItems && !AllItemsCollected, animateGates);
        }

        // Objeto de esta sala ya guardado en una visita anterior: sus puertas quedan abiertas.
        if (room.CollectibleCount > 0 && room.AllCollected) room.UnlockDoors();

        // Con todos los objetos, la salida esta abierta.
        if (AllItemsCollected) room.UnlockFinalExit();
    }

    void BeginPlaying()
    {
        State = GameState.Playing;
        player.InputEnabled = true;
        ignoreDoorsUntil = Time.time + doorCooldown;

        // Si la sala tiene oleadas sin completar, empiezan ahora (al entrar, al reaparecer o al reintentar).
        if (currentRoom != null) currentRoom.NotifyEntered();
    }

    IEnumerator IntroRoutine()
    {
        if (iris != null)
        {
            iris.SnapClosed(player.transform);
            yield return new WaitForSeconds(irisHoldTime);
            yield return iris.Open(player.transform, irisOpenTime);
        }
        BeginPlaying();
    }

    /// Lo llama Room cuando el jugador recoge TODOS los objetos de esa sala.
    public void OnRoomItemsCollected(Room room)
    {
        if (room != currentRoom) return;

        room.UnlockDoors();

        if (AllItemsCollected)
        {
            foreach (var r in allRooms) r.UnlockFinalExit();
            ApplyProgress(room, true);
            ui.ShowMessage("¡Tienes todos los objetos! Busca la salida", 3.5f);
        }
        else
        {
            ui.ShowMessage("Objeto " + ItemsCollected + "/" + totalItems, 2f);
        }
        RefreshHud();
    }

    /// Lo llama Room cuando una sala queda COMPLETADA (oleadas y/o puzzle resueltos).
    public void OnRoomCompleted(Room room)
    {
        if (room != currentRoom) return;
        ui.ShowMessage("¡Sala completada!", 2f);
    }

    /// Lo llama Collectible al ser recogido: el objeto aparece en la UI.
    public void OnCollectibleCollected(Collectible collectible)
    {
        ui.AddCollectedItem(collectible);
    }

    /// Lo llama Collectible cuando reaparece (moriste antes de salir de la sala): sale de la UI.
    public void OnCollectibleRestored(Collectible collectible)
    {
        ui.RemoveCollectedItem(collectible);
    }

    /// Lo llama RoomDoor cuando el jugador toca una puerta ya abierta.
    public void OnDoorEntered(RoomDoor door)
    {
        if (State != GameState.Playing) return;
        if (Time.time < ignoreDoorsUntil) return;

        switch (door.Role)
        {
            case DoorRole.Forward:
                Room next = currentRoom.GetNeighbor(door.Side);
                if (next == null) return;
                StartCoroutine(SlideRoutine(next, door.Side));
                break;

            case DoorRole.FinalExit:
                WinGame();
                break;

            // DoorRole.Back: ya no se usa, se ignora
        }
    }

    /// Congela el juego, desliza la camara a la sala vecina mientras el jugador avanza por la puerta y reanuda.
    /// La sala nueva ya esta pegada a la actual, asi que no hay teletransporte.
    IEnumerator SlideRoutine(Room next, DoorSide side)
    {
        State = GameState.Transition;
        player.InputEnabled = false;

        Room previous = currentRoom;
        Vector2 dir = side.ToWorldDirection();

        // La sala nueva se enciende y se prepara, pero congelada hasta que termine el deslizamiento.
        next.gameObject.SetActive(true);
        next.ResetRoom();
        ApplyProgress(next);

        // Hasta donde avanza el jugador: pasando la puerta de llegada de la sala nueva.
        Vector3 playerStart = player.transform.position;
        Vector3 playerEnd = playerStart + (Vector3)(dir * walkInDistance);
        RoomDoor arrival = next.GetDoor(side.Opposite());
        if (arrival != null)
        {
            Vector3 target = playerStart;
            Vector2 doorPos = arrival.transform.position;
            if (Mathf.Abs(dir.x) > 0.5f) target.x = doorPos.x + dir.x * walkInDistance;
            else target.y = doorPos.y + dir.y * walkInDistance;

            // Solo si de verdad avanza hacia adelante
            if (Vector2.Dot((Vector2)(target - playerStart), dir) > 0.1f) playerEnd = target;
        }

        Vector3 camStart = GetCameraBase();
        Vector3 camTarget = new Vector3(next.CameraPosition.x, next.CameraPosition.y, camStart.z);

        Time.timeScale = 0f;      // pausa: trampas, flechas y enemigos quietos
        try
        {
            float t = 0f;
            float duration = Mathf.Max(0.01f, slideDuration);
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                SetCameraBase(Vector3.Lerp(camStart, camTarget, k));
                player.transform.position = Vector3.Lerp(playerStart, playerEnd, k);
                yield return null;
            }
        }
        finally
        {
            Time.timeScale = 1f;  // despausa siempre, aunque algo interrumpa la corutina
        }

        player.transform.position = playerEnd;
        var body = player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = playerEnd;
            body.linearVelocity = Vector2.zero;
        }
        Physics2D.SyncTransforms();

        // Se guardan los objetos de la sala que dejamos y se cambia de sala.
        previous.BankCollectibles();
        previous.gameObject.SetActive(false);

        currentRoom = next;
        UpdateRoomMusic(next);
        if (next.Type == RoomType.Safe) lastSafeRoom = next;
        entryPosition = playerEnd;      // aqui reapareces si mueres en esta sala
        timeLeft = next.TimeLimit;
        RefreshHud();
        BeginPlaying();
    }

    // ---------- Cinematicas cortas (sala completada) ----------
    /// Congela el control del jugador sin pausar el juego. Devuelve false si no se estaba jugando.
    public bool BeginCutscene()
    {
        if (State != GameState.Playing) return false;
        State = GameState.Transition;
        player.InputEnabled = false;
        return true;
    }

    /// Devuelve el control al jugador tras la cinematica.
    public void EndCutscene()
    {
        if (State != GameState.Transition) return;
        State = GameState.Playing;
        player.InputEnabled = true;
        ignoreDoorsUntil = Time.time + doorCooldown;
        RefreshHud();
    }

    // ---------- Muerte ----------
    /// Lo llama PlayerHealth al morir. hitDirection = hacia donde sale empujado el jugador.
    public void OnPlayerDied(Vector2 hitDirection = default)
    {
        if (State != GameState.Playing) return;

        State = GameState.Dead;
        Deaths++;
        RefreshHud();
        StartCoroutine(RespawnRoutine(hitDirection));
    }

    IEnumerator RespawnRoutine(Vector2 hitDirection)
    {
        // Hitstop + camara lenta con zoom y shake; al terminar el tiempo queda congelado.
        if (gameFeel != null) yield return gameFeel.DeathSequence(player.transform, hitDirection);

        // Tiempo REAL: con el juego congelado (timeScale 0) un WaitForSeconds normal no avanzaria.
        yield return new WaitForSecondsRealtime(respawnDelay);

        ResetRoomAndRespawn(resetTimerOnDeath);
        if (gameFeel != null) gameFeel.Release();    // el tiempo vuelve cuando el jugador ya reaparecio
    }

    float RespawnInvulnerabilityFor(Room room)
    {
        return room != null && room.RespawnInvulnerability > 0f ? room.RespawnInvulnerability : respawnInvulnerability;
    }

    void ResetRoomAndRespawn(bool resetTimer, bool begin = true)
    {
        // Sala del jefe (Keep State On Death): nada se reinicia y se reaparece en el punto mas lejano al jefe
        if (currentRoom.KeepStateOnDeath && State == GameState.Dead)
        {
            Vector2 threat = BossController.Active != null ? (Vector2)BossController.Active.transform.position : (Vector2)entryPosition;
            health.Revive(currentRoom.FarthestRespawnPoint(threat), RespawnInvulnerabilityFor(currentRoom));
            if (begin) BeginPlaying();
            RefreshHud();
            return;
        }

        currentRoom.ResetRoom();
        ApplyProgress(currentRoom);
        health.Revive(entryPosition, RespawnInvulnerabilityFor(currentRoom));
        if (resetTimer) timeLeft = currentRoom.TimeLimit;
        if (begin) BeginPlaying();
        RefreshHud();
    }

    // ---------- Musica segun la sala ----------
    void UpdateRoomMusicForce() => UpdateRoomMusic(currentRoom, true);

    void UpdateRoomMusic(Room room, bool force = false)
    {
        var am = AudioManager.Instance;
        if (am == null || room == null) return;
        if (!force && (State == GameState.GameOver || State == GameState.Win)) return;

        bool boss = room.IsBossRoom && am.HasMusic(MusicId.Boss);
        am.PlayMusic(boss ? MusicId.Boss : MusicId.Gameplay, roomMusicFade);

        bool bossAmb = room.IsBossRoom && am.HasAmbience(AmbienceId.BossRoom);
        am.PlayAmbience(bossAmb ? AmbienceId.BossRoom : AmbienceId.Dungeon, 2f);
    }

    // ---------- Tiempo / Game Over / Victoria ----------
    void OnTimeUp()
    {
        if (gameFeel != null) gameFeel.Release();

        bool toCheckpoint = false;
        // Con el jefe: se vuelve a la sala segura mientras queden reintentos; sin reintentos, a empezar de cero
        if (currentRoom != null && currentRoom.IsBossRoom)
        {
            if (bossRetriesLeft > 0 && lastSafeRoom != null) toCheckpoint = true;
            else mustRestartGame = true;
        }

        // Secuencia del saco (zoom al jugador + el saco le cae encima) y despues checkpoint o Game Over
        if (timeUpSequence != null)
        {
            player.InputEnabled = false;
            timeUpSequence.Play(health, () =>
            {
                if (toCheckpoint) BossCheckpoint();
                else GameOverWithFade();
            });
            if (State == GameState.Playing) State = GameState.Transition;     // por si no hubo zoom
            return;
        }

        if (toCheckpoint)
        {
            BossCheckpoint();
            return;
        }

        // Sin saco: la musica se apaga, fundido a negro y aparece el Game Over (que pone su propia musica)
        player.InputEnabled = false;
        State = GameState.Transition;
        Sfx.Play(SfxId.TimeUp);
        Sfx.StopMusic(timeUpMusicFade);
        Sfx.Ambience(AmbienceId.None, timeUpMusicFade);
        GameOverWithFade();
    }

    // Con el Game Over / la Victoria en pantalla la sala se apaga: trampas, flechas y enemigos dejan de funcionar
    // y de sonar. Al reintentar se vuelve a encender (y se reinicia).
    void PauseWorld(bool paused)
    {
        if (currentRoom != null && currentRoom.gameObject.activeSelf == paused)
            currentRoom.gameObject.SetActive(!paused);
        if (paused && AudioManager.Instance != null) AudioManager.Instance.StopAllSfx();
    }

    void GameOverWithFade()
    {
        ScreenFader f = Fader;
        if (f == null)
        {
            if (timeUpSequence != null) timeUpSequence.Cleanup();
            ShowGameOverNow();
            return;
        }
        // El saco ya hizo su efecto: sin la espera previa del boton
        f.FadeOutThen(onBlack: () =>
        {
            if (timeUpSequence != null) timeUpSequence.Cleanup();
            ShowGameOverNow();
        }, fadeBackIn: true);
    }

    void ShowGameOverNow()
    {
        PauseWorld(true);
        State = GameState.GameOver;
        Deaths++;
        player.InputEnabled = false;
        RefreshHud();
        ui.ShowGameOver();
    }

    /// Boton "Reintentar" del panel de Game Over: reinicia SOLO la sala actual (el mapa no cambia).
    public void RetryRoom()
    {
        if (State != GameState.GameOver) return;
        if (mustRestartGame)
        {
            RestartGame();
            return;
        }

        ScreenFader f = Fader;
        if (f == null)
        {
            ui.HidePanels();
            PauseWorld(false);
            ResetRoomAndRespawn(true);
            return;
        }

        // Efecto del boton -> fundido lento a negro -> la sala se reinicia con la pantalla tapada -> se aclara -> se juega.
        f.FadeOutThen(
            onBlack: () =>
            {
                ui.HidePanels();
                PauseWorld(false);
                UpdateRoomMusicForce();
                ResetRoomAndRespawn(true, false);
                State = GameState.Transition;          // sin control hasta que la pantalla se vea
                player.InputEnabled = false;
            },
            fadeBackIn: true,
            onVisible: BeginPlaying);
    }

    // ---------- Jefe ----------
    /// Se acabo el tiempo con el jefe y quedan reintentos: fundido y de vuelta a la sala segura (el jefe se reinicia).
    void BossCheckpoint()
    {
        bossRetriesLeft--;
        State = GameState.Transition;
        player.InputEnabled = false;
        Deaths++;
        RefreshHud();
        ui.ShowMessage(bossRetriesLeft > 0
            ? "¡El Ropavejero te atrapo! Te quedan " + bossRetriesLeft + " intentos"
            : "¡El Ropavejero te atrapo! Este es tu ultimo intento", 3f);

        Room boss = currentRoom;
        Room safe = lastSafeRoom;
        System.Action go = () =>
        {
            if (timeUpSequence != null) timeUpSequence.Cleanup();
            boss.ResetRoom();                    // el jefe vuelve a empezar con toda su vida
            boss.gameObject.SetActive(false);
            EnterRoom(safe, safe.SpawnPosition, false);
        };

        ScreenFader f = Fader;
        if (f == null)
        {
            go();
            BeginPlaying();
            return;
        }
        f.FadeOutThen(onBlack: go, fadeBackIn: true, onVisible: BeginPlaying);
    }

    /// Lo llama el jefe cuando lo derrotan: fin del juego.
    public void OnBossDefeated() => WinGame();

    /// Lo llama la puerta del jefe cuando el jugador choca con ella y aun faltan objetos.
    public void OnGateBlocked()
    {
        if (Time.time < nextGateHint || State != GameState.Playing) return;
        nextGateHint = Time.time + gateHintCooldown;
        Sfx.Play(SfxId.GateBlocked, player.transform.position);
        ui.ShowMessage("Te faltan recuerdos: " + ItemsCollected + "/" + totalItems, 2f);
    }

    void WinGame()
    {
        if (gameFeel != null) gameFeel.Release();
        State = GameState.Win;
        player.InputEnabled = false;
        Sfx.Ambience(AmbienceId.None, 1.5f);
        PauseWorld(true);
        RefreshHud();
        ui.ShowWin(Deaths, totalTime, ItemsCollected, totalItems);
    }

    /// Boton "Jugar de nuevo": recarga la escena, asi que MapLoader elige otro mapa al azar.
    public void RestartGame()
    {
        ScreenFader f = Fader;
        if (f != null)
        {
            f.ReloadScene();      // espera, fundido lento a negro y recarga
            return;
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void RefreshHud()
    {
        if (currentRoom == null) return;
        ui.SetDeaths(Deaths);
        ui.SetItems(ItemsCollected, totalItems);
        if (currentRoom.HasTimeLimit) ui.SetTime(timeLeft);
        else ui.SetNoTime();
    }
}
