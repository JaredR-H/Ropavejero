using System.Collections;
using UnityEngine;

public class Collectible : MonoBehaviour, IResettable
{
    [Header("Flotar")]
    [SerializeField] float bobHeight = 0.12f;
    [SerializeField] float bobSpeed = 3f;

    [Header("Sombra")]
    [Tooltip("Sprite de la sombra (por ejemplo, el mismo que usa el jugador). Si esta vacio se genera una elipse suave.")]
    [SerializeField] Sprite shadowSprite;
    [Tooltip("Donde queda la sombra respecto a la posicion de reposo del objeto.")]
    [SerializeField] Vector2 shadowOffset = new Vector2(0f, -0.25f);
    [Tooltip("Tamano de la sombra en unidades del mundo (cuando el objeto esta abajo).")]
    [SerializeField] Vector2 shadowSize = new Vector2(0.7f, 0.35f);
    [Tooltip("Tamano de la sombra cuando el objeto esta en lo mas alto (1 = igual que abajo).")]
    [SerializeField, Range(0.2f, 1f)] float shadowScaleAtTop = 0.65f;
    [SerializeField, Range(0f, 1f)] float shadowAlphaAtBottom = 0.45f;
    [SerializeField, Range(0f, 1f)] float shadowAlphaAtTop = 0.25f;
    [SerializeField] Color shadowColor = Color.black;

    [Header("Efecto al recogerlo")]
    [Tooltip("Cuanto dura el estiramiento antes de desaparecer.")]
    [SerializeField, Min(0.05f)] float pickupDuration = 0.4f;
    [Tooltip("CURVA DE ALTURA (aplastar). Eje X: tiempo del efecto (0 a 1). Eje Y: cuanto se aplasta (0 = nada, 1 = queda en una linea fina). " +
             "Si baja de 0 el objeto se estira en vez de aplastarse.")]
    [SerializeField] AnimationCurve pickupFlattenCurve = DefaultFlattenCurve();
    [Tooltip("Cuanto se aplasta con la curva en 1 (0,98 = queda al 2% de alto). Sube o baja el valor sin tocar la curva.")]
    [SerializeField, Range(0f, 0.99f)] float pickupFlattenAmount = 0.98f;
    [Tooltip("CURVA DE ANCHO. Eje X: tiempo del efecto (0 a 1). Eje Y: ancho del objeto (1 = normal, mayor = mas ancho, 0 = desaparece). " +
             "Por defecto se ensancha un poco al aplastarse y despues se recoge hasta un punto, como una television al apagarse.")]
    [SerializeField] AnimationCurve pickupWidthCurve = DefaultWidthCurve();
    [Tooltip("Cuanto blanco tiene el sprite durante el efecto: tiempo (0 a 1) en X, blanco (0 a 1) en Y.")]
    [SerializeField] AnimationCurve pickupWhiteCurve = DefaultWhiteCurve();
    [Tooltip("Desde que punto del efecto (0 a 1) el sprite empieza a desvanecerse. Asi desaparece sobre la linea fina sin que se note.")]
    [SerializeField, Range(0f, 1f)] float pickupFadeStart = 0.65f;
    [Tooltip("Opcional: material del shader Ropavejero/SpriteFlash. Si esta vacio se crea solo.")]
    [SerializeField] Material flashMaterial;

    [Header("UI (objetos recogidos)")]
    [Tooltip("Sprite que aparece en la UI al recogerlo. Si esta vacio se usa el de su SpriteRenderer.")]
    [SerializeField] Sprite uiIcon;
    [Tooltip("Color del borde de su icono en la UI (por ejemplo, otro color para los perks).")]
    [SerializeField] Color uiBorderColor = Color.white;

    Vector3 startLocalPos;
    Room room;
    SpriteRenderer spriteRenderer;
    JuiceAnimator juice;
    SpriteFlasher flasher;
    SpriteRenderer shadowRenderer;
    Color spriteBaseColor = Color.white;
    Vector3 shadowBaseScale = Vector3.one;
    Collider2D[] colliders;
    Coroutine pickupRoutine;
    bool collecting;
    float lastHeight01;

    public bool Collected { get; private set; }
    public bool Banked { get; private set; }
    public Color UiBorderColor => uiBorderColor;

    public Sprite UiIcon
    {
        get
        {
            if (uiIcon != null) return uiIcon;
            return spriteRenderer != null ? spriteRenderer.sprite : null;
        }
    }

    // Television que se apaga, paso 1: se aplasta rapido hasta quedar en una linea (35% del efecto)
    static AnimationCurve DefaultFlattenCurve()
    {
        return JuiceCurves.FromFunction(x =>
        {
            float u = Mathf.Clamp01(x / 0.35f);
            return 1f - Mathf.Pow(1f - u, 3f);
        }, 24);
    }

    // Paso 2: la linea se ensancha un poco y luego se recoge hacia el centro hasta desaparecer (50% a 90% del efecto)
    static AnimationCurve DefaultWidthCurve()
    {
        return JuiceCurves.FromFunction(x =>
        {
            float widen = 1f + 0.35f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x / 0.35f));
            float collapse = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - 0.5f) / 0.4f));
            return widen * collapse;
        }, 32);
    }

    static AnimationCurve DefaultWhiteCurve()
    {
        var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.6f, 1f), new Keyframe(1f, 1f));
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
        return curve;
    }

    void Awake()
    {
        startLocalPos = transform.localPosition;
        room = GetComponentInParent<Room>();
        if (room == null)
            Debug.LogWarning("Collectible '" + name + "' no esta dentro de ningun Room: no abrira ninguna puerta.", this);

        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null) spriteBaseColor = spriteRenderer.color;
        colliders = GetComponents<Collider2D>();

        // Squash & stretch y blanco del sprite (se anaden solos si faltan)
        juice = GetComponent<JuiceAnimator>();
        if (juice == null) juice = gameObject.AddComponent<JuiceAnimator>();
        juice.Configure(null, false, false);

        if (spriteRenderer != null)
        {
            flasher = spriteRenderer.GetComponent<SpriteFlasher>();
            if (flasher == null) flasher = spriteRenderer.gameObject.AddComponent<SpriteFlasher>();
            flasher.SetMaterial(flashMaterial);
        }

        BuildShadow();
    }

    void OnEnable()
    {
        if (shadowRenderer != null) shadowRenderer.gameObject.SetActive(true);
    }

    void OnDisable()
    {
        if (shadowRenderer != null) shadowRenderer.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (shadowRenderer != null) Destroy(shadowRenderer.gameObject);
    }

    // La sombra se queda en el suelo (hermana del objeto, no hija), porque el objeto flota.
    void BuildShadow()
    {
        Sprite sprite = shadowSprite != null ? shadowSprite : ProceduralSprites.Ellipse;

        var go = new GameObject(name + " Shadow");
        bool hasParent = transform.parent != null;
        go.transform.SetParent(hasParent ? transform.parent : transform, false);
        go.transform.localPosition = (hasParent ? startLocalPos : Vector3.zero) + (Vector3)shadowOffset;

        shadowRenderer = go.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = sprite;
        if (spriteRenderer != null)
        {
            shadowRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            shadowRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        }

        Vector2 size = sprite.bounds.size;
        shadowBaseScale = new Vector3(shadowSize.x / Mathf.Max(0.0001f, size.x),
                                      shadowSize.y / Mathf.Max(0.0001f, size.y), 1f);
        go.transform.localScale = shadowBaseScale;
        UpdateShadow(0f, 1f);
    }

    void UpdateShadow(float height01, float fade)
    {
        if (shadowRenderer == null) return;

        shadowRenderer.transform.localScale = shadowBaseScale * (Mathf.Lerp(1f, shadowScaleAtTop, height01) * fade);
        Color c = shadowColor;
        c.a = Mathf.Lerp(shadowAlphaAtBottom, shadowAlphaAtTop, height01) * fade;
        shadowRenderer.color = c;
    }

    void Update()
    {
        if (collecting) return;

        float s = Mathf.Sin(Time.time * bobSpeed);
        transform.localPosition = startLocalPos + Vector3.up * (s * bobHeight);

        // Cuanto mas alto, mas pequena y clara la sombra
        lastHeight01 = (s + 1f) * 0.5f;
        UpdateShadow(lastHeight01, 1f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (Collected) return;
        if (!other.TryGetComponent(out PlayerController _)) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        Collected = true;
        if (GameManager.Instance != null) GameManager.Instance.OnCollectibleCollected(this);
        if (room != null) room.OnCollectibleTaken();

        pickupRoutine = StartCoroutine(PickupRoutine());
    }

    IEnumerator PickupRoutine()
    {
        collecting = true;
        SetCollidersEnabled(false);       // para que no se pueda recoger dos veces

        float t = 0f;
        while (t < pickupDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / pickupDuration);

            // Altura y ancho controlados por las curvas del inspector (efecto de television que se apaga)
            if (juice != null)
            {
                float height = 1f - pickupFlattenCurve.Evaluate(k) * pickupFlattenAmount;
                float width = pickupWidthCurve.Evaluate(k);
                juice.SetOverlayScale(Mathf.Max(0.01f, width), Mathf.Max(0.01f, height));
            }

            if (flasher != null) flasher.SetFlashAmount(pickupWhiteCurve.Evaluate(k));
            UpdateShadow(lastHeight01, 1f - k);      // la sombra se desvanece

            // Cuando ya es una linea fina, se desvanece: asi no hay un "desaparece de golpe"
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pickupFadeStart, 1f, k));
            SetSpriteAlpha(fade);
            yield return null;
        }

        SetSpriteAlpha(0f);
        if (flasher != null) flasher.SetFlashAmount(0f);
        pickupRoutine = null;
        gameObject.SetActive(false);
    }

    /// Guarda el objeto como recogido de forma permanente (lo llama Room al salir con vida).
    public void Bank()
    {
        if (Collected) Banked = true;
    }

    public void ResetState()
    {
        if (Banked) return;     // ya esta guardado: se queda recogido

        if (Collected && GameManager.Instance != null) GameManager.Instance.OnCollectibleRestored(this);

        if (pickupRoutine != null) StopCoroutine(pickupRoutine);
        pickupRoutine = null;
        collecting = false;
        Collected = false;

        if (juice != null)
        {
            juice.StopAll();
            juice.ClearOverlay();
        }
        if (flasher != null) flasher.Cancel();
        SetSpriteAlpha(1f);
        SetCollidersEnabled(true);

        transform.localPosition = startLocalPos;
        gameObject.SetActive(true);
    }

    void SetSpriteAlpha(float alpha)
    {
        if (spriteRenderer == null) return;
        Color c = spriteBaseColor;
        c.a *= alpha;
        spriteRenderer.color = c;
    }

    void SetCollidersEnabled(bool enabled)
    {
        if (colliders == null) return;
        foreach (var c in colliders)
            if (c != null) c.enabled = enabled;
    }
}
