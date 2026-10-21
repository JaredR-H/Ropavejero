using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Playing, Dead, Transition, GameOver, Win }

/// Flujo lineal: sala 1 -> 2 -> 3 -> ... -> última sala.
///  - Salas de introducción: puerta con "Always Open", sin objetos.
///  - Salas con objeto: la puerta se abre al recogerlo.
///  - Última sala: al recoger su objeto se abre la puerta de victoria en esa misma sala.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] Room[] rooms;
    [SerializeField] PlayerController player;
    [SerializeField] Camera mainCamera;
    [SerializeField] UIManager ui;
    [SerializeField] IrisTransition iris;          // opcional: si está vacío, el cambio de sala es instantáneo

    [Header("Reglas")]
    [Tooltip("Si está activo, al morir en una trampa el tiempo de la sala vuelve a empezar.")]
    [SerializeField] bool resetTimerOnDeath = true;
    [SerializeField] float respawnDelay = 0.6f;

    [Header("Transición de iris")]
    [SerializeField] float irisCloseTime = 0.45f;
    [SerializeField] float irisHoldTime = 0.15f;   // tiempo con la pantalla cerrada
    [SerializeField] float irisOpenTime = 0.5f;

    PlayerHealth health;
    int currentIndex;
    int totalItems;
    float timeLeft;
    float totalTime;

    public GameState State { get; private set; } = GameState.Transition;
    public bool IsPlaying => State == GameState.Playing;
    public int Deaths { get; private set; }

    Room CurrentRoom => rooms[currentIndex];
    bool IsLastRoom => currentIndex >= rooms.Length - 1;

    public int ItemsCollected
    {
        get
        {
            int n = 0;
            foreach (var r in rooms) n += r.CollectedCount;
            return n;
        }
    }

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

        // Todas las salas están activas en el editor para poder trabajar; en juego solo vive la actual.
        foreach (var r in rooms)
        {
            totalItems += r.CollectibleCount;
            r.gameObject.SetActive(false);
        }

        ui.HidePanels();
        EnterRoom(0, false);
        StartCoroutine(IntroRoutine());
    }

    void Update()
    {
        if (State != GameState.Playing) return;

        totalTime += Time.deltaTime;
        if (!CurrentRoom.HasTimeLimit) return;

        timeLeft -= Time.deltaTime;
        ui.SetTime(timeLeft);

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            ui.SetTime(0f);
            OnTimeUp();
        }
    }

    // ---------- Salas ----------
    /// startPlaying = false deja al jugador congelado (estado Transition) hasta que termine el iris.
    void EnterRoom(int index, bool startPlaying = true)
    {
        currentIndex = index;
        var room = CurrentRoom;

        room.gameObject.SetActive(true);
        room.ResetRoom();

        Vector3 camPos = room.CameraPosition;
        mainCamera.transform.position = new Vector3(camPos.x, camPos.y, -10f);

        health.Revive(room.SpawnPosition);
        timeLeft = room.TimeLimit;

        if (startPlaying) State = GameState.Playing;
        else
        {
            State = GameState.Transition;
            player.InputEnabled = false;
        }
        RefreshHud();
    }

    void BeginPlaying()
    {
        State = GameState.Playing;
        player.InputEnabled = true;
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
        if (room != CurrentRoom) return;

        room.UnlockDoors();
        if (IsLastRoom) ui.ShowMessage("¡Se abrió la salida! Escapa", 3.5f);
        RefreshHud();
    }

    /// Lo llama RoomDoor cuando el jugador toca una puerta ya abierta.
    public void OnDoorEntered(RoomDoor door)
    {
        if (State != GameState.Playing) return;

        switch (door.Role)
        {
            case DoorRole.Forward:
                if (IsLastRoom) WinGame();
                else StartCoroutine(TransitionRoutine(currentIndex + 1));
                break;

            case DoorRole.FinalExit:
                if (IsLastRoom) WinGame();
                break;

                // DoorRole.Back: ya no se usa, se ignora
        }
    }

    IEnumerator TransitionRoutine(int nextIndex)
    {
        State = GameState.Transition;
        player.InputEnabled = false;

        // 1) El círculo se cierra sobre el jugador (en la sala vieja)
        if (iris != null) yield return iris.Close(player.transform, irisCloseTime);

        // 2) Con la pantalla tapada: cambio de sala (cámara + jugador al Spawn)
        CurrentRoom.gameObject.SetActive(false);
        EnterRoom(nextIndex, false);
        if (iris != null) yield return new WaitForSeconds(irisHoldTime);

        // 3) El círculo se abre sobre el jugador en la sala nueva
        if (iris != null) yield return iris.Open(player.transform, irisOpenTime);

        BeginPlaying();
    }

    // ---------- Muerte ----------
    public void OnPlayerDied()
    {
        if (State != GameState.Playing) return;

        State = GameState.Dead;
        Deaths++;
        RefreshHud();
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        ResetRoomAndRespawn(resetTimerOnDeath);
    }

    void ResetRoomAndRespawn(bool resetTimer)
    {
        CurrentRoom.ResetRoom();
        health.Revive(CurrentRoom.SpawnPosition);
        if (resetTimer) timeLeft = CurrentRoom.TimeLimit;
        State = GameState.Playing;
        RefreshHud();
    }

    // ---------- Tiempo / Game Over / Victoria ----------
    void OnTimeUp()
    {
        State = GameState.GameOver;
        Deaths++;
        player.InputEnabled = false;
        RefreshHud();
        ui.ShowGameOver();
    }

    /// Botón "Reintentar" del panel de Game Over: reinicia SOLO la sala actual.
    public void RetryRoom()
    {
        if (State != GameState.GameOver) return;
        ui.HidePanels();
        ResetRoomAndRespawn(true);
    }

    void WinGame()
    {
        State = GameState.Win;
        player.InputEnabled = false;
        RefreshHud();
        ui.ShowWin(Deaths, totalTime, ItemsCollected, totalItems);
    }

    /// Botón "Jugar de nuevo" del panel de victoria (la escena debe estar en Build Profiles > Scene List).
    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void RefreshHud()
    {
        ui.SetDeaths(Deaths);
        ui.SetItems(ItemsCollected, totalItems);
        if (CurrentRoom.HasTimeLimit) ui.SetTime(timeLeft);
        else ui.SetNoTime();
    }
}