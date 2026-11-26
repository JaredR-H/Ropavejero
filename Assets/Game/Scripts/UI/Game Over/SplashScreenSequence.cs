using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// Escena de inicio propia (en vez del splash de Unity): muestra los logos uno tras otro con fundido
/// ("Made with Unity", el logo del estudio...) y ARRANCA LA MUSICA DEL MENU desde el primer segundo.
/// Mientras tanto carga el menu principal por detras, asi no hay tirones al cambiar de escena.
/// Debe ser la PRIMERA escena de la lista (Build Profiles > Scene List, posicion 0).
public class SplashScreenSequence : MonoBehaviour
{
    [System.Serializable]
    public class Logo
    {
        [Tooltip("CanvasGroup del logo (empieza en alpha 0).")]
        public CanvasGroup group;
        public float fadeIn = 0.6f;
        public float hold = 1.6f;
        public float fadeOut = 0.6f;
        [Tooltip("Crece un poquito mientras se ve (1 = nada, 1.05 = 5%). Da vida.")]
        public float grow = 1.04f;
    }

    [SerializeField] Logo[] logos;
    [Tooltip("Negro antes del primer logo.")]
    [SerializeField] float startDelay = 0.5f;
    [Tooltip("Pausa en negro entre un logo y el siguiente.")]
    [SerializeField] float gap = 0.35f;
    [Tooltip("Negro al final, antes de pasar al menu.")]
    [SerializeField] float endDelay = 0.3f;
    [SerializeField] string nextScene = "MainMenu";

    [Header("Musica")]
    [SerializeField] MusicId music = MusicId.Menu;
    [Tooltip("Cuando arranca la musica (segundos desde que abre el juego).")]
    [SerializeField] float musicStartDelay = 0f;

    [Header("Saltar")]
    [Tooltip("Pulsar una tecla salta al siguiente logo (la musica sigue igual).")]
    [SerializeField] bool skippable = true;

    bool skipLogo;
    float elapsed;
    bool musicStarted;

    void Start()
    {
        foreach (var l in logos)
            if (l != null && l.group != null) l.group.alpha = 0f;
        StartCoroutine(Run());
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);   // sin saltos por el tiron de carga
        elapsed += dt;
        if (!musicStarted && elapsed >= musicStartDelay)
        {
            musicStarted = true;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(music, 0f);
        }
        if (skippable && AnyPressed()) skipLogo = true;
    }

    IEnumerator Run()
    {
        // Carga el menu en segundo plano mientras se ven los logos
        AsyncOperation load = SceneManager.LoadSceneAsync(nextScene);
        if (load != null) load.allowSceneActivation = false;

        yield return null;
        yield return null;
        yield return Wait(startDelay);

        foreach (var l in logos)
        {
            if (l == null || l.group == null) continue;
            skipLogo = false;
            Transform tr = l.group.transform;
            Vector3 baseScale = tr.localScale;
            float total = l.fadeIn + l.hold + l.fadeOut;
            float t = 0f;
            while (t < total)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
                t += dt;
                if (skipLogo && t < l.fadeIn + l.hold) t = l.fadeIn + l.hold;     // salta directo al fundido de salida

                float a = t < l.fadeIn ? t / Mathf.Max(0.01f, l.fadeIn)
                        : t < l.fadeIn + l.hold ? 1f
                        : 1f - (t - l.fadeIn - l.hold) / Mathf.Max(0.01f, l.fadeOut);
                a = Mathf.Clamp01(a);
                l.group.alpha = a * a * (3f - 2f * a);
                tr.localScale = baseScale * Mathf.Lerp(1f, l.grow, t / total);
                yield return null;
            }
            l.group.alpha = 0f;
            tr.localScale = baseScale;
            yield return Wait(gap);
        }

        yield return Wait(endDelay);
        if (load != null) load.allowSceneActivation = true;
        else SceneManager.LoadScene(nextScene);
    }

    static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
            yield return null;
        }
    }

    static bool AnyPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
        var m = Mouse.current;
        return m != null && m.leftButton.wasPressedThisFrame;
    }
}
