using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] SpriteRenderer visualRenderer;
    [Tooltip("Tine al jugador de rojo al morir. Desmarcalo si tienes animacion de muerte.")]
    [SerializeField] bool tintOnDeath = true;
    [SerializeField] Color deathColor = new Color(1f, 0.25f, 0.25f);

    [Header("Invulnerabilidad (al reaparecer en la sala del jefe, al escapar del saco...)")]
    [Tooltip("Parpadeos por segundo del blanco suave mientras es invulnerable.")]
    [SerializeField] float invulnerableBlinkSpeed = 6f;
    [Tooltip("Cuanto blanco llega a tener en cada parpadeo (0 a 1).")]
    [SerializeField, Range(0f, 1f)] float invulnerableBlinkAmount = 0.7f;

    [Header("Golpe mortal")]
    [Tooltip("Fuerza con la que sale empujado el jugador al ser golpeado.")]
    [SerializeField] float deathKnockbackForce = 9f;
    [Tooltip("Donde salen las particulas del golpe respecto al pivote (el centro del cuerpo).")]
    [SerializeField] Vector2 hitVfxOffset = new Vector2(0f, 0.4f);
    [SerializeField] float hitVfxScale = 1f;

    PlayerAttack attack;
    PlayerAnimator anim;
    Animator visualAnimator;
    Color baseColor = Color.white;
    SpriteFlasher flasher;
    float invulnerableUntil;
    bool blinking;
    bool captured;

    public PlayerController Controller { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsInvulnerable => Time.time < invulnerableUntil;
    public bool IsCaptured => captured;
    public SpriteRenderer VisualRenderer => visualRenderer;

    void Awake()
    {
        Controller = GetComponent<PlayerController>();
        attack = GetComponent<PlayerAttack>();
        anim = GetComponent<PlayerAnimator>();
        visualAnimator = GetComponentInChildren<Animator>(true);
        if (visualRenderer != null)
        {
            baseColor = visualRenderer.color;
            flasher = visualRenderer.GetComponent<SpriteFlasher>();
            if (flasher == null) flasher = visualRenderer.gameObject.AddComponent<SpriteFlasher>();
        }
    }

    // Parpadeo blanco suave mientras es invulnerable
    void Update()
    {
        if (IsInvulnerable && !IsDead)
        {
            blinking = true;
            float wave = 0.5f - 0.5f * Mathf.Cos(Time.time * invulnerableBlinkSpeed * Mathf.PI * 2f);
            if (flasher != null) flasher.SetFlashAmount(wave * invulnerableBlinkAmount);
        }
        else if (blinking)
        {
            blinking = false;
            if (flasher != null) flasher.SetFlashAmount(0f);
        }
    }

    /// Nada le hace dano durante estos segundos (parpadea en blanco).
    public void GrantInvulnerability(float seconds)
    {
        if (seconds <= 0f) return;
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
    }

    /// Atrapado en el saco: nada le hace dano salvo ForceDie.
    public void SetCaptured(bool value) => captured = value;

    /// Muerte que ignora la invulnerabilidad y el estar atrapado (por ejemplo, no escapar del saco a tiempo).
    public void ForceDie(Vector2 sourcePosition) => DieInternal(sourcePosition, true, true);

    /// Muerte sin origen conocido (trampas antiguas): empuje contrario al ultimo movimiento.
    public void Die() => DieInternal(Vector2.zero, false, false);

    /// Muerte con origen conocido: empuje alejandose de sourcePosition.
    public void Die(Vector2 sourcePosition) => DieInternal(sourcePosition, true, false);

    void DieInternal(Vector2 source, bool hasSource, bool force)
    {
        if (IsDead) return;
        if (!force && (captured || IsInvulnerable)) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
        if (Controller.IsInvulnerable) return;

        // En una sala COMPLETADA nada hace dano (aunque quede alguna trampa sin agrupar en un TrapSet)
        if (GameManager.Instance != null && GameManager.Instance.CurrentRoom != null && GameManager.Instance.CurrentRoom.IsCompleted) return;

        IsDead = true;
        captured = false;
        if (Controller.IsHeld) Controller.Hold(false);

        Vector2 dir = hasSource ? (Vector2)transform.position - source : -Controller.LastMoveDirection;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
        dir.Normalize();

        if (attack != null) attack.CancelAttack();
        Controller.InputEnabled = false;
        Controller.ApplyKnockback(dir, deathKnockbackForce);

        Vfx.Play(VfxKind.HitPlayer, (Vector2)transform.position + hitVfxOffset, dir, hitVfxScale);

        if (tintOnDeath && visualRenderer != null) visualRenderer.color = deathColor;
        if (anim != null) anim.SetDead(true);
        SetAnimatorUnscaled(true);   // la animacion de muerte sigue durante el tiempo congelado

        if (GameManager.Instance != null) GameManager.Instance.OnPlayerDied(dir);
    }

    public void Revive(Vector3 position, float invulnerableSeconds = 0f)
    {
        IsDead = false;
        captured = false;
        invulnerableUntil = 0f;
        GrantInvulnerability(invulnerableSeconds);
        if (visualRenderer != null) visualRenderer.color = baseColor;
        if (anim != null) anim.SetDead(false);
        SetAnimatorUnscaled(false);
        if (attack != null) attack.CancelAttack();
        Controller.TeleportTo(position);
        Controller.InputEnabled = true;
    }

    void SetAnimatorUnscaled(bool unscaled)
    {
        if (visualAnimator != null)
            visualAnimator.updateMode = unscaled ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
    }
}
