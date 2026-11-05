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
    [SerializeField] IrisTransition iris;          // opcional: solo se usa al empezar la partida
    [Tooltip("Opcional: dirige la camara (shake, zoom), el hitstop, la camara lenta y el congelado al morir. Sin el, el juego funciona sin esos efectos.")]
    [SerializeField] GameFeel gameFeel;
    [Tooltip("Opcional: fundido a negro lento al reiniciar. Si esta vacio se usa el ScreenFader de la escena (si hay).")]
    [SerializeField] ScreenFader fader;

    [Header("Reglas")]
    [Tooltip("Si esta activo, al morir en una trampa el tiempo de la sala vuelve a empezar.")]
    [SerializeField] bool resetTimerOnDeath = true;
    [SerializeField] float respawnDelay = 0.6f;
    [Tooltip("Tras entrar a una sala se ignoran las puertas este tiempo, para no rebotar de vuelta.")]
    [SerializeField] float doorCooldown = 0.4f;

    [Header("Deslizamiento entre salas")]
    [Tooltip("Segundos que dura el movimiento de camara (el juego esta congelado mientras tanto).")]
    [SerializeField] float slideDuration = 0.6f;
    [Tooltip("Cuanto avanza el jugador DENTRO de la sala nueva, medido desde el centro de su puerta de llegada. Debe quedar fuera del trigger de esa puerta.")]
    [SerializeField] float walkInDistance = 1.6f;

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
        EnterRoom(first, first.SpawnPosition, false);
        StartCoroutine(IntroRoutine());
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
        entryPosition = entry;

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
    void ApplyProgress(Room room)
    {
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
            ui.ShowMessage("¡Tienes todos los objetos! Busca la salida", 3.5f);
        }
        else
        {
            ui.ShowMessage("Objeto " + ItemsCollected + "/" + totalItems, 2f);
        }
        RefreshHud();
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
        entryPosition = playerEnd;      // aqui reapareces si mueres en esta sala
        timeLeft = next.TimeLimit;
        RefreshHud();
        BeginPlaying();
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

    void ResetRoomAndRespawn(bool resetTimer, bool begin = true)
    {
        currentRoom.ResetRoom();
        ApplyProgress(currentRoom);
        health.Revive(entryPosition);
        if (resetTimer) timeLeft = currentRoom.TimeLimit;
        if (begin) BeginPlaying();
        RefreshHud();
    }

    // ---------- Tiempo / Game Over / Victoria ----------
    void OnTimeUp()
    {
        if (gameFeel != null) gameFeel.Release();
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

        ScreenFader f = Fader;
        if (f == null)
        {
            ui.HidePanels();
            ResetRoomAndRespawn(true);
            return;
        }

        // Efecto del boton -> fundido lento a negro -> la sala se reinicia con la pantalla tapada -> se aclara -> se juega.
        f.FadeOutThen(
            onBlack: () =>
            {
                ui.HidePanels();
                ResetRoomAndRespawn(true, false);
                State = GameState.Transition;          // sin control hasta que la pantalla se vea
                player.InputEnabled = false;
            },
            fadeBackIn: true,
            onVisible: BeginPlaying);
    }

    void WinGame()
    {
        if (gameFeel != null) gameFeel.Release();
        State = GameState.Win;
        player.InputEnabled = false;
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
