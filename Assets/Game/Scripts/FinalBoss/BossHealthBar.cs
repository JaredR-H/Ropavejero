using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossHealthBar : MonoBehaviour
{
    public static BossHealthBar Instance { get; private set; }

    [Header("Partes (vacio = se crean solas)")]
    [SerializeField] RectTransform root;
    [SerializeField] Image fill;
    [SerializeField] Image trail;
    [SerializeField] TMP_Text nameText;

    [Header("Aspecto (solo si se crea sola)")]
    [SerializeField] Vector2 size = new Vector2(640f, 22f);
    [SerializeField] float bottomMargin = 40f;
    [SerializeField] Color backColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] Color fillColor = new Color(0.85f, 0.15f, 0.2f);
    [SerializeField] Color trailColor = new Color(1f, 0.95f, 0.85f);

    [Header("Animacion")]
    [Tooltip("Segundos que espera la estela blanca antes de bajar.")]
    [SerializeField] float trailDelay = 0.35f;
    [SerializeField] float trailSpeed = 0.8f;

    [Header("Vibracion al recibir dano")]
    [Tooltip("Pixeles maximos que se mueve la barra en cada golpe normal.")]
    [SerializeField] float shakeStrength = 8f;
    [Tooltip("Duracion de la vibracion (segundos reales, ignora el hitstop).")]
    [SerializeField] float shakeDuration = 0.25f;
    [Tooltip("Cambios de direccion por segundo. Mas alto = vibracion mas nerviosa.")]
    [SerializeField] float shakeFrequency = 45f;
    [Tooltip("Cuanto se mueve en Y comparado con X (0 = solo horizontal).")]
    [Range(0f, 1f)] [SerializeField] float shakeVertical = 0.5f;
    [Tooltip("Golpes que quitan mas vida vibran mas fuerte (hasta este multiplicador).")]
    [SerializeField] float maxShakeMultiplier = 2f;

    float target = 1f;
    float trailValue = 1f;
    float trailWait;
    JuiceAnimator juice;
    CanvasGroup group;
    float fadeTime;
    float fadeDuration;

    Vector2 basePosition;
    Vector2 shakeOffset;
    Vector2 shakeDir;
    float shakeTime;
    float shakeTotal;
    float shakeAmp;
    float shakeTick;

    public static BossHealthBar Get()
    {
        if (Instance != null) return Instance;
        Instance = FindFirstObjectByType<BossHealthBar>();
        if (Instance == null) Instance = new GameObject("BossHealthBar").AddComponent<BossHealthBar>();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (root == null || fill == null) Build();
        if (root != null)
        {
            juice = root.GetComponent<JuiceAnimator>();
            if (juice == null) juice = root.gameObject.AddComponent<JuiceAnimator>();
            juice.Configure(null, true, false);
            group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            basePosition = root.anchoredPosition;
            root.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// Muestra la barra. fadeIn = segundos (reales) del fade in; 0 = aparece de golpe.
    public void Show(string bossName, float fadeIn = 0f)
    {
        if (root == null) return;
        if (nameText != null) nameText.text = bossName;
        target = trailValue = 1f;
        StopShake();
        Apply();
        fadeDuration = Mathf.Max(0f, fadeIn);
        fadeTime = 0f;
        if (group != null) group.alpha = fadeDuration > 0f ? 0f : 1f;
        root.gameObject.SetActive(true);
    }

    public void Hide()
    {
        StopShake();
        if (root != null) root.gameObject.SetActive(false);
    }

    /// Hace vibrar la barra. multiplier = 1 es un golpe normal.
    public void Shake(float multiplier = 1f)
    {
        if (root == null || multiplier <= 0f) return;
        shakeAmp = Mathf.Max(shakeAmp * Remaining01(), shakeStrength * multiplier);
        shakeTotal = shakeTime = Mathf.Max(0.01f, shakeDuration);
        shakeTick = 0f;
    }

    float Remaining01() => shakeTotal > 0f ? Mathf.Clamp01(shakeTime / shakeTotal) : 0f;

    void StopShake()
    {
        shakeTime = 0f;
        shakeOffset = Vector2.zero;
        if (root != null) root.anchoredPosition = basePosition;
    }

    /// 0 = sin vida, 1 = vida completa.
    public void SetValue(float value01)
    {
        value01 = Mathf.Clamp01(value01);
        if (value01 < target)
        {
            trailWait = trailDelay;
            if (juice != null) juice.Play("NumberBounce");
            // 10% de vida o mas = vibracion maxima; golpes chicos = vibracion normal
            float lost = target - value01;
            Shake(Mathf.Lerp(1f, maxShakeMultiplier, Mathf.InverseLerp(0.02f, 0.1f, lost)));
        }
        target = value01;
        if (trailValue < target) trailValue = target;
        Apply();
    }

    void Update()
    {
        if (root == null || !root.gameObject.activeSelf) return;
        if (trailWait > 0f) trailWait -= Time.unscaledDeltaTime;
        else if (trailValue > target) trailValue = Mathf.MoveTowards(trailValue, target, trailSpeed * Time.unscaledDeltaTime);
        UpdateShake(Time.unscaledDeltaTime);
        if (group != null && fadeTime < fadeDuration)
        {
            fadeTime += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(fadeTime / fadeDuration);
            group.alpha = k * k * (3f - 2f * k);                // suave al inicio y al final
        }
        Apply();
    }

    void UpdateShake(float dt)
    {
        if (shakeTime <= 0f)
        {
            if (shakeOffset != Vector2.zero) StopShake();
            return;
        }
        shakeTime -= dt;
        // cambia de direccion a ritmo fijo (sacudida seca, no temblor suave)
        shakeTick -= dt;
        if (shakeTick <= 0f)
        {
            shakeTick = 1f / Mathf.Max(1f, shakeFrequency);
            // alterna lado en X para que se sienta como golpe, Y al azar
            float side = shakeDir.x > 0f ? -1f : 1f;
            shakeDir = new Vector2(side * Random.Range(0.6f, 1f), Random.Range(-1f, 1f) * shakeVertical);
        }
        float k = Remaining01();
        shakeOffset = shakeDir * (shakeAmp * k * k);
        root.anchoredPosition = basePosition + shakeOffset;
        if (shakeTime <= 0f) StopShake();
    }

    void Apply()
    {
        if (fill != null) fill.fillAmount = target;
        if (trail != null) trail.fillAmount = trailValue;
    }

    void Build()
    {
        var canvasGo = new GameObject("BossHealthCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var rootGo = new GameObject("Root", typeof(RectTransform));
        root = (RectTransform)rootGo.transform;
        root.SetParent(canvasGo.transform, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = new Vector2(0f, bottomMargin);
        root.sizeDelta = size;
        basePosition = root.anchoredPosition;

        MakeImage("Back", root, backColor, false);
        trail = MakeImage("Trail", root, trailColor, true);
        fill = MakeImage("Fill", root, fillColor, true);

        var textGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        var trt = (RectTransform)textGo.transform;
        trt.SetParent(root, false);
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 0f);
        trt.anchoredPosition = new Vector2(0f, 6f);
        trt.sizeDelta = new Vector2(0f, 36f);
        nameText = textGo.GetComponent<TextMeshProUGUI>();
        nameText.alignment = TextAlignmentOptions.Bottom;
        nameText.fontSize = 30f;
        nameText.color = Color.white;
        nameText.raycastTarget = false;
    }

    static Image MakeImage(string objectName, RectTransform parent, Color color, bool filled)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = filled ? new Vector2(3f, 3f) : Vector2.zero;
        rt.offsetMax = filled ? new Vector2(-3f, -3f) : Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (filled)
        {
            // Image Filled necesita un sprite; se crea uno blanco
            img.sprite = WhiteSprite();
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = 0;
            img.fillAmount = 1f;
        }
        return img;
    }

    static Sprite white;
    static Sprite WhiteSprite()
    {
        if (white != null) return white;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        white = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
        white.hideFlags = HideFlags.HideAndDontSave;
        return white;
    }
}
