using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// Intro del menu principal (portada por capas):
///  1. Fundido desde negro mientras la camara hace un zoom out: el titi entra desde la izquierda haciendose mas pequeno,
///     el humo entra desde un lado y el Ropavejero aparece en la puerta (transparente -> color, y creciendo).
///  2. El logo cae desde la camara con squash & stretch; al "ensamblarse" sale un flash blanco y la pantalla tiembla.
///  3. "Pulsa cualquier tecla para continuar" parpadea.
///  4. Al pulsar, los botones entran uno tras otro desde la derecha con squash & stretch.
/// Pulsar una tecla durante la intro la salta (queda todo en su sitio y aparece el texto).
/// Todo se anima a partir de cuanto tiempo lleva la intro, asi que saltarla siempre deja todo exacto.
/// La POSICION FINAL de cada cosa es donde la dejes en la escena: la intro solo anima desde fuera hasta ahi.
public class MainMenuIntro : MonoBehaviour
{
    [Header("Modo")]
    [Tooltip("Marcado: empieza solo al cargar la escena (menu). Desmarcado: espera a que alguien llame a Play() (Game Over, Victoria).")]
    [SerializeField] bool playOnStart = true;
    [Tooltip("Marcado: espera 'Pulsa cualquier tecla' antes de los botones. Desmarcado: los botones entran solos tras el logo.")]
    [SerializeField] bool waitForKey = true;

    [Header("Sincronizar con la musica")]
    [Tooltip("Musica con la que se sincroniza el impacto del logo (y que arranca si no esta sonando).")]
    [SerializeField] MusicId music = MusicId.Menu;
    [Tooltip("Segundo de la cancion en el que debe caer el logo (el golpe/drop de la musica). -1 = sin sincronizar " +
             "(la musica igual arranca si no esta sonando).")]
    [SerializeField] float musicTimeAtImpact = -1f;
    [Tooltip("Fundido de entrada de la musica si la arranca este script (0 = de golpe).")]
    [SerializeField] float musicFadeIn = 0f;

    [Header("Sonidos")]
    [SerializeField] bool playSounds = true;
    [SerializeField] SfxId impactSound = SfxId.LogoImpact;
    [SerializeField] SfxId continueSound = SfxId.PressStart;
    [SerializeField] SfxId buttonSound = SfxId.MenuWhoosh;

    [Header("Pantalla")]
    [Tooltip("CanvasGroup de una Image NEGRA a pantalla completa (encima de todo).")]
    [SerializeField] CanvasGroup black;
    [Tooltip("CanvasGroup de una Image BLANCA a pantalla completa (encima de todo, empieza en alpha 0).")]
    [SerializeField] CanvasGroup flash;
    [SerializeField] float blackFadeTime = 1.6f;
    [Tooltip("Segundos en negro antes de empezar (deja que la escena termine de cargar).")]
    [SerializeField] float startDelay = 0.35f;
    [Tooltip("Durante este tiempo al inicio no se puede saltar la intro (evita que el clic de Play o una tecla la salte).")]
    [SerializeField] float ignoreInputTime = 0.6f;

    [Header("Camara (zoom out)")]
    [SerializeField] Camera menuCamera;
    [Tooltip("Tamano de la camara al empezar comparado con el final (0.85 = empieza 15% mas cerca).")]
    [SerializeField, Range(0.5f, 1f)] float cameraStartZoom = 0.86f;
    [SerializeField] float cameraZoomTime = 3.4f;

    [Header("Titi (personaje)")]
    [Tooltip("El sprite del titi (HIJO de su capa de parallax).")]
    [SerializeField] Transform titi;
    [Tooltip("Opcional: objeto vacio (hermano del sprite) en el centro del personaje. El zoom se hace alrededor de ese punto.")]
    [SerializeField] Transform titiPivot;
    [Tooltip("De donde viene, respecto a su posicion final (unidades). Negativo en X = desde la izquierda.")]
    [SerializeField] Vector2 titiStartOffset = new Vector2(-5f, -0.6f);
    [Tooltip("Tamano al empezar (1.6 = 60% mas grande: parece mas cerca de la camara).")]
    [SerializeField] float titiStartScale = 1.6f;
    [SerializeField] float titiDelay = 0.15f;
    [SerializeField] float titiTime = 3.2f;

    [Header("Ropavejero (silueta)")]
    [SerializeField] SpriteRenderer boss;
    [Tooltip("Opcional: objeto vacio (hermano del sprite) a los PIES del Ropavejero. Crece desde ahi.")]
    [SerializeField] Transform bossPivot;
    [SerializeField] float bossStartScale = 0.72f;
    [SerializeField] float bossDelay = 1.1f;
    [SerializeField] float bossTime = 2.3f;

    [Header("Humo")]
    [Tooltip("Sprites de humo/niebla que entran desde un lado (con MenuFog o sin el).")]
    [SerializeField] SpriteRenderer[] smoke;
    [Tooltip("Desde donde entra el humo, respecto a su posicion final (unidades). Positivo en X = desde la derecha.")]
    [SerializeField] Vector2 smokeStartOffset = new Vector2(6f, 0f);
    [SerializeField] float smokeDelay = 0.4f;
    [SerializeField] float smokeTime = 3f;

    [Header("Logo (cae desde la camara)")]
    [SerializeField] RectTransform logo;
    [Tooltip("CanvasGroup del logo (para su fade). Vacio = se agrega solo.")]
    [SerializeField] CanvasGroup logoGroup;
    [Tooltip("Cuando empieza a caer el logo (segundos desde el inicio).")]
    [SerializeField] float logoTime = 3.3f;
    [SerializeField] float logoFallTime = 0.42f;
    [Tooltip("Tamano del logo al empezar la caida (grande = viene desde la camara).")]
    [SerializeField] float logoStartScale = 3f;
    [Tooltip("Desde donde cae, respecto a su posicion final (pixeles).")]
    [SerializeField] Vector2 logoStartOffset = new Vector2(0f, 220f);
    [SerializeField] float logoStartRotation = -10f;
    [Tooltip("Fuerza del aplastamiento al caer (0.3 = se aplasta 30%).")]
    [SerializeField, Range(0f, 0.6f)] float logoSquash = 0.32f;
    [Tooltip("Vibraciones del rebote por segundo.")]
    [SerializeField] float logoBounceFrequency = 3.2f;
    [Tooltip("Que tan rapido se calma el rebote.")]
    [SerializeField] float logoBounceDamping = 6.5f;

    [Header("Impacto del logo")]
    [SerializeField] float flashFadeTime = 0.7f;
    [Range(0f, 1f)] [SerializeField] float flashAlpha = 1f;
    [Tooltip("Objeto que tiembla con el impacto (el padre de todas las capas del mundo).")]
    [SerializeField] Transform artShakeRoot;
    [Tooltip("Objeto de UI que tiembla con el impacto (padre del logo y los botones). Opcional.")]
    [SerializeField] RectTransform uiShakeRoot;
    [SerializeField] float shakeArt = 0.18f;
    [SerializeField] float shakeUi = 14f;
    [SerializeField] float shakeTime = 0.45f;

    [Header("Pulsa cualquier tecla")]
    [Tooltip("CanvasGroup del texto 'Pulsa cualquier tecla para continuar'.")]
    [SerializeField] CanvasGroup pressText;
    [Tooltip("Segundos despues del impacto del logo.")]
    [SerializeField] float pressTextDelay = 0.9f;
    [SerializeField] Vector2 pressTextAlpha = new Vector2(0.25f, 1f);
    [SerializeField] float pressTextBlinkPeriod = 1.6f;
    [SerializeField] float pressTextFadeOut = 0.25f;

    [Header("Botones")]
    [Tooltip("El HUECO de cada boton (padre del boton con JuiceButton), en el orden en que entran.")]
    [SerializeField] RectTransform[] buttonSlots;
    [Tooltip("CanvasGroup del contenedor de los botones: no se pueden pulsar hasta que entran todos.")]
    [SerializeField] CanvasGroup buttonsGroup;
    [Tooltip("Desde donde entran (pixeles, respecto a su sitio). Positivo en X = desde la derecha, fuera de pantalla.")]
    [SerializeField] Vector2 buttonStartOffset = new Vector2(1100f, 0f);
    [SerializeField] float buttonStagger = 0.12f;
    [SerializeField] float buttonTravelTime = 0.5f;
    [Tooltip("Cuanto se pasa de su sitio antes de volver (rebote al llegar).")]
    [SerializeField] float buttonOvershoot = 1.6f;
    [SerializeField, Range(0f, 0.5f)] float buttonSquash = 0.18f;

    [Header("Parallax")]
    [Tooltip("Cuanto tarda el parallax del mouse en activarse del todo tras el impacto del logo.")]
    [SerializeField] float parallaxRampTime = 1.2f;

    [Header("Eventos (sonidos, musica...)")]
    public UnityEvent onLogoImpact;
    public UnityEvent onPressTextShown;
    public UnityEvent onContinuePressed;
    public UnityEvent onButtonsReady;

    enum Phase { Intro, WaitingKey, Buttons, Ready }

    Phase phase = Phase.Intro;
    float t;                    // tiempo de la intro
    float buttonsT;             // tiempo desde que se pulso una tecla
    bool impacted;
    float impactAt = -1f;
    bool skipped;
    float waited;
    int framesAlive;
    bool playing;
    int buttonsWhooshed;
    bool musicChecked;

    // Estados finales (los de la escena)
    float camSize;
    Vector3 titiPos, titiScale, titiPivotLocal;
    Vector3 bossPos, bossScale, bossPivotLocal;
    Color bossColor;
    Vector3[] smokePos;
    MenuFog[] smokeFog;
    Color[] smokeColor;
    Vector2 logoPos;
    Vector3 logoScale;
    Vector2[] slotPos;
    Vector3 artRootPos;
    Vector2 uiRootPos;

    float ImpactTime => logoTime + logoFallTime;

    void Awake()
    {
        if (menuCamera == null) menuCamera = Camera.main;
        if (menuCamera != null) camSize = menuCamera.orthographicSize;

        if (titi != null)
        {
            titiPos = titi.localPosition;
            titiScale = titi.localScale;
            titiPivotLocal = titiPivot != null && titi.parent != null ? titi.parent.InverseTransformPoint(titiPivot.position) : titiPos;
        }
        if (boss != null)
        {
            bossPos = boss.transform.localPosition;
            bossScale = boss.transform.localScale;
            bossColor = boss.color;
            var bt = boss.transform;
            bossPivotLocal = bossPivot != null && bt.parent != null ? bt.parent.InverseTransformPoint(bossPivot.position) : bossPos;
        }
        if (smoke != null)
        {
            smokePos = new Vector3[smoke.Length];
            smokeFog = new MenuFog[smoke.Length];
            smokeColor = new Color[smoke.Length];
            for (int i = 0; i < smoke.Length; i++)
            {
                if (smoke[i] == null) continue;
                smokePos[i] = smoke[i].transform.localPosition;
                smokeFog[i] = smoke[i].GetComponent<MenuFog>();
                smokeColor[i] = smoke[i].color;
            }
        }
        if (logo != null)
        {
            logoPos = logo.anchoredPosition;
            logoScale = logo.localScale;
            if (logoGroup == null) logoGroup = logo.GetComponent<CanvasGroup>();
            if (logoGroup == null) logoGroup = logo.gameObject.AddComponent<CanvasGroup>();
            logoGroup.blocksRaycasts = false;
        }
        if (buttonSlots != null)
        {
            slotPos = new Vector2[buttonSlots.Length];
            for (int i = 0; i < buttonSlots.Length; i++)
                if (buttonSlots[i] != null) slotPos[i] = buttonSlots[i].anchoredPosition;
        }
        if (artShakeRoot != null) artRootPos = artShakeRoot.localPosition;
        if (uiShakeRoot != null) uiRootPos = uiShakeRoot.anchoredPosition;

        if (black != null) { black.alpha = 1f; black.blocksRaycasts = true; }
        if (flash != null) { flash.alpha = 0f; flash.blocksRaycasts = false; }
        if (pressText != null) pressText.alpha = 0f;
        if (buttonsGroup != null) { buttonsGroup.interactable = false; buttonsGroup.blocksRaycasts = false; }
        MenuParallaxLayer.GlobalWeight = 0f;

        playing = playOnStart;
        Apply();
    }

    /// Reproduce la secuencia desde el principio (Game Over / Victoria). Activa antes el objeto que la contiene.
    public void Play()
    {
        playing = true;
        phase = Phase.Intro;
        t = 0f;
        buttonsT = 0f;
        impacted = false;
        impactAt = -1f;
        skipped = false;
        waited = 0f;
        framesAlive = 2;
        buttonsWhooshed = 0;
        musicChecked = false;
        if (buttonsGroup != null) { buttonsGroup.interactable = false; buttonsGroup.blocksRaycasts = false; }
        Apply();
    }

    // La musica es el reloj: el logo cae justo en 'musicTimeAtImpact'. Devuelve false si hay que esperar en negro.
    bool SyncWithMusic()
    {
        var am = AudioManager.Instance;
        if (am == null || music == MusicId.None) return true;

        if (am.CurrentMusic != music || !am.IsMusicPlaying)
        {
            if (musicChecked && am.CurrentMusic == music) return true;     // ya la pedimos (sin clip): no insistir
            musicChecked = true;
            float start = musicTimeAtImpact >= 0f ? Mathf.Max(0f, musicTimeAtImpact - ImpactTime - t) : 0f;
            am.PlayMusic(music, musicFadeIn, start);
            return true;
        }
        musicChecked = true;
        if (musicTimeAtImpact < 0f || skipped || t >= ImpactTime) return true;

        float desired = ImpactTime - (musicTimeAtImpact - am.MusicTime);
        if (desired < 0f) return false;            // la musica va antes del momento: esperar en negro
        t = desired;
        return true;
    }

    void StartButtons()
    {
        phase = Phase.Buttons;
        buttonsT = 0f;
        buttonsWhooshed = 0;
    }

    void OnDestroy() => MenuParallaxLayer.GlobalWeight = 1f;

    void Update()
    {
        // El primer frame tras cargar la escena puede traer un salto de varios segundos (sobre todo en el Editor):
        // se ignoran los primeros frames y se limita el paso para que la intro no se "coma" ese tiempo.
        if (!playing) return;
        framesAlive++;
        if (framesAlive <= 2) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);

        if (waited < startDelay)
        {
            waited += dt;
            return;
        }

        bool pressed = AnyPressed() && waited >= startDelay && t >= ignoreInputTime;

        switch (phase)
        {
            case Phase.Intro:
            {
                float before = t;
                if (!SyncWithMusic())
                {
                    Apply();
                    return;
                }
                if (t == before) t += dt;
                if (pressed) SkipIntro();
                if (t >= ImpactTime + pressTextDelay)
                {
                    if (waitForKey)
                    {
                        phase = Phase.WaitingKey;
                        onPressTextShown?.Invoke();
                    }
                    else StartButtons();
                }
                break;
            }

            case Phase.WaitingKey:
                t += dt;
                if (pressed)
                {
                    StartButtons();
                    if (playSounds) Sfx.Play(continueSound);
                    onContinuePressed?.Invoke();
                }
                break;

            case Phase.Buttons:
                t += dt;
                buttonsT += dt;
                if (buttonsT >= ButtonsDuration)
                {
                    phase = Phase.Ready;
                    if (buttonsGroup != null) { buttonsGroup.interactable = true; buttonsGroup.blocksRaycasts = true; }
                    onButtonsReady?.Invoke();
                }
                break;

            case Phase.Ready:
                t += dt;
                break;
        }

        if (!impacted && t >= ImpactTime) Impact(!skipped);
        Apply();
    }

    float ButtonsDuration
    {
        get
        {
            int n = buttonSlots != null ? buttonSlots.Length : 0;
            return Mathf.Max(0, n - 1) * buttonStagger + buttonTravelTime + 0.35f;
        }
    }

    void SkipIntro()
    {
        skipped = true;
        t = Mathf.Max(t, ImpactTime + pressTextDelay);
    }

    void Impact(bool withFeel)
    {
        impacted = true;
        impactAt = withFeel ? ImpactTime : -999f;    // saltada: sin flash ni temblor
        if (withFeel && playSounds) Sfx.Play(impactSound);
        onLogoImpact?.Invoke();
    }

    // ---------- Todo se calcula a partir del tiempo ----------
    void Apply()
    {
        // Negro
        if (black != null)
        {
            black.alpha = 1f - Smooth(Progress(t, 0f, blackFadeTime));
            black.blocksRaycasts = black.alpha > 0.01f;
        }

        // Camara: zoom out
        if (menuCamera != null && camSize > 0f)
            menuCamera.orthographicSize = camSize * Mathf.Lerp(cameraStartZoom, 1f, EaseOutCubic(Progress(t, 0f, cameraZoomTime)));

        // Titi: desde la izquierda, grande -> su sitio
        if (titi != null)
        {
            float k = EaseOutCubic(Progress(t, titiDelay, titiTime));
            float s = Mathf.Lerp(titiStartScale, 1f, k);
            Vector3 offset = Vector3.Lerp(titiStartOffset, Vector2.zero, k);
            titi.localPosition = titiPivotLocal + (titiPos - titiPivotLocal) * s + offset;
            titi.localScale = titiScale * s;
        }

        // Ropavejero: transparente -> color, creciendo desde los pies
        if (boss != null)
        {
            float k = Progress(t, bossDelay, bossTime);
            float s = Mathf.Lerp(bossStartScale, 1f, EaseOutCubic(k));
            boss.transform.localPosition = bossPivotLocal + (bossPos - bossPivotLocal) * s;
            boss.transform.localScale = bossScale * s;
            Color c = bossColor;
            c.a = bossColor.a * Smooth(k);
            boss.color = c;
        }

        // Humo: entra desde un lado
        if (smoke != null)
        {
            float k = Progress(t, smokeDelay, smokeTime);
            Vector3 offset = Vector3.Lerp(smokeStartOffset, Vector2.zero, EaseOutCubic(k));
            float vis = Smooth(k);
            for (int i = 0; i < smoke.Length; i++)
            {
                if (smoke[i] == null) continue;
                if (smokeFog[i] != null)
                {
                    smokeFog[i].ExtraOffset = offset;
                    smokeFog[i].Visibility = vis;
                }
                else
                {
                    smoke[i].transform.localPosition = smokePos[i] + offset;
                    Color c = smokeColor[i];
                    c.a *= vis;
                    smoke[i].color = c;
                }
            }
        }

        ApplyLogo();
        ApplyFlashAndShake();

        // Texto "pulsa cualquier tecla"
        if (pressText != null)
        {
            float a = 0f;
            float shownAt = ImpactTime + pressTextDelay;
            if (phase == Phase.Buttons || phase == Phase.Ready)
            {
                a = Mathf.Clamp01(1f - buttonsT / Mathf.Max(0.01f, pressTextFadeOut)) * pressTextAlpha.y;
            }
            else if (t >= shownAt)
            {
                float since = t - shownAt;
                float blink = 0.5f + 0.5f * Mathf.Cos(since * Mathf.PI * 2f / pressTextBlinkPeriod);
                a = Mathf.Lerp(pressTextAlpha.x, pressTextAlpha.y, blink) * Mathf.Clamp01(since / 0.3f);
            }
            pressText.alpha = a;
        }

        ApplyButtons();

        // Parallax del mouse: se activa poco a poco despues del impacto
        MenuParallaxLayer.GlobalWeight = Smooth(Progress(t, ImpactTime, parallaxRampTime));
    }

    void ApplyLogo()
    {
        if (logo == null) return;

        if (t < logoTime)
        {
            logoGroup.alpha = 0f;
            return;
        }

        if (t < ImpactTime)
        {
            // Cae acelerando desde la camara
            float k = Progress(t, logoTime, logoFallTime);
            float e = k * k;
            logo.anchoredPosition = logoPos + Vector2.Lerp(logoStartOffset, Vector2.zero, e);
            logo.localScale = logoScale * Mathf.Lerp(logoStartScale, 1f, e);
            logo.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(logoStartRotation, 0f, e));
            logoGroup.alpha = Mathf.Clamp01(k * 3f);
            return;
        }

        // Ensamblado: se aplasta y rebota hasta quedar quieto (resorte amortiguado)
        float since = t - ImpactTime;
        float spring = Mathf.Exp(-since * logoBounceDamping) * Mathf.Cos(since * logoBounceFrequency * Mathf.PI * 2f);
        float sx = 1f + logoSquash * spring;
        float sy = 1f - logoSquash * spring;
        logo.anchoredPosition = logoPos + new Vector2(0f, -logoSquash * 40f * spring);
        logo.localScale = new Vector3(logoScale.x * sx, logoScale.y * sy, logoScale.z);
        logo.localRotation = Quaternion.Euler(0f, 0f, logoStartRotation * 0.15f * spring);
        logoGroup.alpha = 1f;
    }

    void ApplyFlashAndShake()
    {
        float since = t - impactAt;
        bool active = impactAt > -100f && since >= 0f;

        if (flash != null)
            flash.alpha = active ? flashAlpha * (1f - Smooth(Mathf.Clamp01(since / Mathf.Max(0.01f, flashFadeTime)))) : 0f;

        float k = active ? Mathf.Clamp01(1f - since / Mathf.Max(0.01f, shakeTime)) : 0f;
        k *= k;
        Vector2 n = k > 0f
            ? new Vector2(Mathf.PerlinNoise(3f, t * 38f) - 0.5f, Mathf.PerlinNoise(7f, t * 38f) - 0.5f) * 2f
            : Vector2.zero;

        if (artShakeRoot != null) artShakeRoot.localPosition = artRootPos + (Vector3)(n * shakeArt * k);
        if (uiShakeRoot != null) uiShakeRoot.anchoredPosition = uiRootPos + n * shakeUi * k;
    }

    void ApplyButtons()
    {
        if (buttonSlots == null) return;
        bool started = phase == Phase.Buttons || phase == Phase.Ready;

        for (int i = 0; i < buttonSlots.Length; i++)
        {
            var slot = buttonSlots[i];
            if (slot == null) continue;

            if (!started)
            {
                slot.anchoredPosition = slotPos[i] + buttonStartOffset;
                slot.localScale = Vector3.one;
                continue;
            }

            float local = buttonsT - i * buttonStagger;
            if (local >= 0f && i >= buttonsWhooshed)
            {
                buttonsWhooshed = i + 1;
                if (playSounds) Sfx.Play(buttonSound, 1f, 1f + i * 0.06f);   // cada boton un poquito mas agudo
            }
            float k = Progress(local, 0f, buttonTravelTime);
            slot.anchoredPosition = slotPos[i] + Vector2.LerpUnclamped(buttonStartOffset, Vector2.zero, EaseOutBack(k, buttonOvershoot));

            // Viaja estirado (va rapido) y al llegar se aplasta y rebota
            float sx, sy;
            if (k < 1f)
            {
                float speed = 1f - k;
                sx = 1f + buttonSquash * speed;
                sy = 1f - buttonSquash * 0.7f * speed;
            }
            else
            {
                float since = local - buttonTravelTime;
                float spring = Mathf.Exp(-since * 9f) * Mathf.Cos(since * 3.5f * Mathf.PI * 2f);
                sx = 1f - buttonSquash * 0.8f * spring;
                sy = 1f + buttonSquash * 0.8f * spring;
            }
            slot.localScale = new Vector3(sx, sy, 1f);
        }
    }

    // ---------- Entrada ----------
    static bool AnyPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return true;
        var pad = Gamepad.current;
        if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame ||
                            pad.buttonEast.wasPressedThisFrame)) return true;
        return false;
    }

    // ---------- Curvas ----------
    static float Progress(float time, float start, float duration)
        => duration <= 0f ? (time >= start ? 1f : 0f) : Mathf.Clamp01((time - start) / duration);

    static float Smooth(float k) => k * k * (3f - 2f * k);

    static float EaseOutCubic(float k)
    {
        float i = 1f - k;
        return 1f - i * i * i;
    }

    static float EaseOutBack(float k, float overshoot)
    {
        float c3 = overshoot + 1f;
        float i = k - 1f;
        return 1f + c3 * i * i * i + overshoot * i * i;
    }
}
