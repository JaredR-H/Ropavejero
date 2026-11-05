using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    /// Carga una escena por su nombre al instante (debe estar en Build Profiles > Scene List).
    public void Load(string sceneName)
    {
        Time.timeScale = 1f; // por si se viene desde la pausa
        SceneManager.LoadScene(sceneName);
    }

    /// Boton "Jugar" o "Menu principal": efecto del boton, fundido lento a negro y carga de la escena.
    public void LoadWithFade(string sceneName)
    {
        if (ScreenFader.Instance != null) ScreenFader.Instance.LoadScene(sceneName);
        else Load(sceneName);
    }

    /// Recarga la escena actual al instante.
    public void ReloadCurrent()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// Boton "Reiniciar" (por ejemplo, el del menu de pausa): fundido lento a negro y recarga de la escena.
    public void ReloadWithFade()
    {
        if (ScreenFader.Instance != null) ScreenFader.Instance.ReloadScene();
        else ReloadCurrent();
    }

    /// Cierra el juego. No hace nada en el editor ni en WebGL.
    public void Quit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
