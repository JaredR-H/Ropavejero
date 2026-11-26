using UnityEngine;

/// Pone una musica y/o un ambiente al empezar la escena (con fundido). Si ya esta sonando esa musica, no la reinicia.
/// Ponlo en un objeto vacio de la escena (MainMenu: Menu. Game: lo maneja el GameManager, no hace falta).
public class SceneMusic : MonoBehaviour
{
    [SerializeField] MusicId music = MusicId.Menu;
    [SerializeField] float musicFade = 1.5f;
    [SerializeField] AmbienceId ambience = AmbienceId.None;
    [SerializeField] float ambienceFade = 2f;

    void Start()
    {
        if (AudioManager.Instance == null) return;
        if (music != MusicId.None) AudioManager.Instance.PlayMusic(music, musicFade);
        AudioManager.Instance.PlayAmbience(ambience, ambienceFade);
    }
}
