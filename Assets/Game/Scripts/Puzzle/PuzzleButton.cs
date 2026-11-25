using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class PuzzleButton : MonoBehaviour, IHittable
{
    public enum PressMode { Hit, Step, HitOrStep }

    [Header("Como se presiona")]
    [Tooltip("Hit = golpearlo con la espada. Step = pisarlo (el collider debe ser Is Trigger). HitOrStep = las dos.")]
    [SerializeField] PressMode pressMode = PressMode.Hit;
    [Tooltip("Marcado: al presionarlo se queda hundido. Desmarcado: vuelve a su sitio (para secuencias).")]
    [SerializeField] bool latch = true;

    [Header("Al presionarlo")]
    [Tooltip("Segundos antes de poder presionarlo otra vez (evita spamear, sobre todo en secuencias).")]
    [SerializeField, Min(0f)] float pressCooldown = 0.35f;
    [Tooltip("Shake de camara al presionarlo (0 a 1). GameFeel eleva este valor al cuadrado: 0,45 es un golpe pequeno pero visible. " +
             "Debe ser menor que el Shake de RoomCompleteCinematic, que es el golpe del boton que resuelve todo.")]
    [SerializeField, Range(0f, 1f)] float pressShake = 0.45f;
    [Tooltip("Hitstop al presionarlo (segundos).")]
    [SerializeField, Min(0f)] float pressHitStop = 0.05f;
    [Tooltip("Tamano de la nube de polvo al presionarlo (0 = sin polvo).")]
    [SerializeField] float pressDustScale = 0.6f;
    [Tooltip("Tamano de las estrellitas al presionarlo (0 = sin estrellas).")]
    [SerializeField] float pressStarsScale = 1f;

    [Header("Aspecto")]
    [SerializeField] SpriteRenderer sr;
    [SerializeField] Sprite idleSprite;
    [SerializeField] Sprite pressedSprite;
    [SerializeField] Color idleColor = Color.white;
    [Tooltip("Color cuando el puzzle lo ilumina (secuencias, avisos).")]
    [SerializeField] Color litColor = new Color(1f, 0.95f, 0.45f);
    [SerializeField] Color pressedColor = new Color(0.65f, 1f, 0.65f);
    [SerializeField] Color wrongColor = new Color(1f, 0.3f, 0.3f);
    [Tooltip("Color cuando todavia no se puede presionar.")]
    [SerializeField] Color disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.85f);
    [Tooltip("Opcional: texto sobre el boton (por ejemplo, el numero de orden).")]
    [SerializeField] TMP_Text label;

    [Header("Eventos")]
    [SerializeField] UnityEvent onPressed;

    public event Action<PuzzleButton> Pressed;

    JuiceAnimator juice;
    Coroutine wrongRoutine;
    Coroutine unlatchRoutine;
    bool pressed;
    bool lit;
    bool wrong;
    bool interactable = true;
    float nextPressTime;

    public bool IsPressed => pressed;
    public bool Interactable => interactable;

    void Awake()
    {
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

        juice = GetComponent<JuiceAnimator>();
        if (juice == null) juice = gameObject.AddComponent<JuiceAnimator>();
        juice.Configure(null, false, false);

        Apply();
    }

    // ---------- Entrada ----------
    public void OnHit(PlayerAttack attacker)
    {
        if (pressMode == PressMode.Step) return;
        TryPress();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (pressMode == PressMode.Hit) return;
        if (!other.TryGetComponent(out PlayerController _)) return;
        TryPress();
    }

    void TryPress()
    {
        if (!interactable) return;
        if (latch && pressed) return;
        if (Time.time < nextPressTime) return;          // cooldown: no se puede spamear
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
        Press();
    }

    void Press()
    {
        pressed = true;
        nextPressTime = Time.time + pressCooldown;
        Apply();
        if (juice != null) juice.Play("Pop");
        // Golpe en CADA pulsacion: shake con empujon hacia el boton + hitstop corto
        if (GameFeel.Instance != null)
        {
            var player = FindFirstObjectByType<PlayerController>();
            Vector2 dir = player != null ? (Vector2)(transform.position - player.transform.position) : Vector2.zero;
            if (pressShake > 0f) GameFeel.Instance.AddShake(pressShake, dir);
            if (pressHitStop > 0f) GameFeel.Instance.HitStop(pressHitStop);
        }
        if (pressDustScale > 0f) Vfx.Play(VfxKind.Dust, transform.position, Vector2.up, pressDustScale);
        if (pressStarsScale > 0f) Vfx.Play(VfxKind.Stars, transform.position, Vector2.up, pressStarsScale);

        Pressed?.Invoke(this);
        onPressed?.Invoke();

        if (!latch)
        {
            if (unlatchRoutine != null) StopCoroutine(unlatchRoutine);
            unlatchRoutine = StartCoroutine(UnpressAfter(0.18f));
        }
    }

    IEnumerator UnpressAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        pressed = false;
        unlatchRoutine = null;
        Apply();
    }

    // ---------- Control desde el puzzle ----------
    public void SetLatch(bool value) => latch = value;

    /// Hundirlo o levantarlo sin que cuente como una pulsacion.
    public void SetPressed(bool value)
    {
        pressed = value;
        Apply();
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
        Apply();
    }

    /// Iluminarlo (por ejemplo, al mostrar una secuencia).
    public void Light(bool on)
    {
        lit = on;
        Apply();
        if (on && juice != null) juice.Play("Pop");
    }

    /// Parpadeo rojo de error.
    public void FlashWrong(float duration = 0.4f)
    {
        if (wrongRoutine != null) StopCoroutine(wrongRoutine);
        wrongRoutine = StartCoroutine(WrongRoutine(duration));
    }

    IEnumerator WrongRoutine(float duration)
    {
        wrong = true;
        Apply();
        if (juice != null) juice.Play("Hit");
        yield return new WaitForSeconds(duration);
        wrong = false;
        wrongRoutine = null;
        Apply();
    }

    public void SetLabel(string text)
    {
        if (label == null) return;
        label.text = text;
        label.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    /// Vuelve a su estado inicial.
    public void ResetButton()
    {
        if (wrongRoutine != null) StopCoroutine(wrongRoutine);
        if (unlatchRoutine != null) StopCoroutine(unlatchRoutine);
        wrongRoutine = null;
        unlatchRoutine = null;
        pressed = false;
        lit = false;
        wrong = false;
        interactable = true;
        if (juice != null) juice.StopAll();
        Apply();
    }

    void Apply()
    {
        if (sr == null) return;

        if (pressedSprite != null || idleSprite != null)
        {
            Sprite s = pressed && pressedSprite != null ? pressedSprite : idleSprite;
            if (s != null) sr.sprite = s;
        }

        Color c;
        if (wrong) c = wrongColor;
        else if (lit) c = litColor;
        else if (pressed) c = pressedColor;
        else if (!interactable) c = disabledColor;
        else c = idleColor;
        sr.color = c;
    }
}
