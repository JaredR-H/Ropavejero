using UnityEngine;

/// Pantalla de Game Over o de Victoria con escenario propio (sprites iluminados, luciernagas, niebla, luz en cono...).
/// El escenario vive en la escena Game, lejos de las salas, con SU PROPIA camara (que se enciende al mostrarlo).
/// La animacion (titulo que cae con squash & stretch, flash, botones que entran) la hace un MainMenuIntro
/// con 'Play On Start' y 'Wait For Key' desmarcados. La musica tambien la arranca ese MainMenuIntro.
public class EndScreen : MonoBehaviour
{
    [Tooltip("Raiz del escenario en el mundo (sprites, luces, niebla, luciernagas y su camara). Empieza DESACTIVADA.")]
    [SerializeField] GameObject stage;
    [Tooltip("Panel de UI con el titulo, el texto y los botones (el gameOverPanel / winPanel de siempre). Empieza desactivado.")]
    [SerializeField] GameObject panel;
    [Tooltip("La secuencia animada (MainMenuIntro en modo pantalla final).")]
    [SerializeField] MainMenuIntro intro;
    [Tooltip("Ambiente que suena con esta pantalla (None = silencio de fondo).")]
    [SerializeField] AmbienceId ambience = AmbienceId.None;

    public bool IsShown { get; private set; }

    void Awake()
    {
        if (stage != null) stage.SetActive(false);
        if (panel != null) panel.SetActive(false);
    }

    public void Show()
    {
        IsShown = true;
        if (stage != null) stage.SetActive(true);
        if (panel != null) panel.SetActive(true);
        if (intro != null)
        {
            if (!intro.gameObject.activeInHierarchy) intro.gameObject.SetActive(true);
            intro.Play();
        }
        Sfx.Ambience(ambience, 1.5f);
    }

    public void Hide()
    {
        IsShown = false;
        if (stage != null) stage.SetActive(false);
        if (panel != null) panel.SetActive(false);
    }
}
