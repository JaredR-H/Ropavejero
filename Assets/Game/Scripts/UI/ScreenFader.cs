using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Fundido a negro lento con una pequena espera previa (como en Resident Evil), para cambiar de escena o reiniciar.
/// Va en un Canvas propio (Screen Space - Overlay, Sort Order alto) que tenga una Image negra a pantalla completa
/// y un CanvasGroup. Funciona con el juego en pausa (usa tiempo real).
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Tooltip("CanvasGroup del panel negro. Si esta vacio se busca en este objeto.")]
    [SerializeField] CanvasGroup group;

    [Header("Al empezar la escena")]
    [Tooltip("Marcado: la escena empieza en negro y se aclara despacio (util en el menu principal).")]
    [SerializeField] bool fadeInOnStart = false;
    [SerializeField] float startFadeInDuration = 1.2f;

    [Header("Al pulsar un boton (Jugar, Reiniciar...)")]
    [Tooltip("Espera antes de empezar a oscurecer: deja ver el efecto del boton.")]
    [SerializeField] float pressDelay = 0.4f;
    [Tooltip("Lo lento que se oscurece la pantalla.")]
    [SerializeField] float fadeOutDuration = 1.8f;
    [Tooltip("Tiempo en negro antes de cambiar de escena o reiniciar.")]
    [SerializeField] float holdBlackDuration = 0.5f;
    [Tooltip("Lo que tarda en aclararse de nuevo (solo cuando no se cambia de escena).")]
    [SerializeField] float fadeInDuration = 1.2f;
    [SerializeField] AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public bool IsBusy { get; private set; }

    void Awake()
    {
        Instance = this;
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            Debug.LogError("ScreenFader: falta el CanvasGroup.", this);
            return;
        }
        group.alpha = fadeInOnStart ? 1f : 0f;
        group.blocksRaycasts = fadeInOnStart;
        group.interactable = false;
    }

    void Start()
    {
        if (fadeInOnStart && group != null) StartCoroutine(StartRoutine());
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    IEnumerator StartRoutine()
    {
        IsBusy = true;
        yield return Fade(1f, 0f, startFadeInDuration);
        group.blocksRaycasts = false;
        IsBusy = false;
    }

    /// Pulsar: espera, oscurece despacio, se queda un momento en negro y entonces ejecuta onBlack.
    /// Si fadeBackIn es true, despues se aclara y llama a onVisible. Si no (cambio de escena), se queda en negro.
    public void FadeOutThen(Action onBlack, bool fadeBackIn = false, Action onVisible = null)
    {
        if (IsBusy || group == null) return;
        StartCoroutine(FadeRoutine(onBlack, fadeBackIn, onVisible));
    }

    /// Carga una escena por nombre con el fundido. Nombre vacio = recargar la actual.
    public void LoadScene(string sceneName)
    {
        FadeOutThen(() =>
        {
            Time.timeScale = 1f;
            if (string.IsNullOrEmpty(sceneName)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            else SceneManager.LoadScene(sceneName);
        });
    }

    public void ReloadScene() => LoadScene(null);

    IEnumerator FadeRoutine(Action onBlack, bool fadeBackIn, Action onVisible)
    {
        IsBusy = true;
        group.blocksRaycasts = true;           // desde el primer momento: no se pueden pulsar mas botones

        yield return WaitUnscaled(pressDelay);
        yield return Fade(0f, 1f, fadeOutDuration);
        yield return WaitUnscaled(holdBlackDuration);

        onBlack?.Invoke();

        if (fadeBackIn)
        {
            yield return Fade(1f, 0f, fadeInDuration);
            group.blocksRaycasts = false;
            IsBusy = false;
            onVisible?.Invoke();
        }
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            group.alpha = Mathf.LerpUnclamped(from, to, fadeCurve.Evaluate(Mathf.Clamp01(t)));
            yield return null;
        }
        group.alpha = to;
    }

    static IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
