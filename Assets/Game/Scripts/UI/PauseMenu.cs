using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Tooltip("Vacío = se busca solo (el UIManager del mismo Canvas).")]
    [SerializeField] UIManager ui;
    [Tooltip("Vacío = se busca solo.")]
    [SerializeField] PlayerController player;
    [Tooltip("Marcado: también pausa el sonido (música incluida).")]
    [SerializeField] bool pauseAudio = false;

    bool paused;

    public bool IsPaused => paused;

    void Awake()
    {
        if (ui == null) ui = GetComponent<UIManager>();
        if (ui == null) ui = FindFirstObjectByType<UIManager>();
        if (player == null) player = FindFirstObjectByType<PlayerController>();
    }

    void Start()
    {
        if (ui != null) ui.HidePause();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
        {
            if (paused) Resume();
            else TryPause();
        }

        // Otros sistemas (hitstop, camara lenta...) no pueden quitar la pausa.
        if (paused && Time.timeScale != 0f) Time.timeScale = 0f;
    }

    void TryPause()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsPlaying) return;
        Pause();
    }

    public void Pause()
    {
        if (paused) return;
        paused = true;

        if (player != null) player.InputEnabled = false;
        Time.timeScale = 0f;
        if (pauseAudio) AudioListener.pause = true;
        if (ui != null) ui.ShowPause();
    }

    public void Resume()
    {
        if (!paused) return;
        paused = false;

        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (ui != null) ui.HidePause();
        if (player != null) player.InputEnabled = true;
    }

    // ---------- Botones (OnClick) ----------

    /// Boton "Reiniciar": nueva partida (el mapa se vuelve a elegir al azar).
    public void RestartGame()
    {
        paused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (ui != null) ui.HidePause();

        if (GameManager.Instance != null) GameManager.Instance.RestartGame();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// Boton "Menu principal": escribe el nombre de la escena en el OnClick, p. ej. MainMenu.
    public void LoadScene(string sceneName)
    {
        paused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(sceneName);
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}