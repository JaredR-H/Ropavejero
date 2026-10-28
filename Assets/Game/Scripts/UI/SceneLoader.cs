using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    /// Carga una escena por su nombre (debe estar en Build Profiles > Scene List).
    public void Load(string sceneName)
    {
        Time.timeScale = 1f; // por si se viene desde la pausa
        SceneManager.LoadScene(sceneName);
    }

    /// Recarga la escena actual.
    public void ReloadCurrent()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
