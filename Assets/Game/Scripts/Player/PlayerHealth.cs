using System.Collections;
using System.Collections.Generic;
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

    [Header("Desaparecer al morir")]
    [Tooltip("Al morir el personaje desaparece (sprite, sombra y espada) tapado por una nube de humo. Vuelve al reaparecer.")]
    [SerializeField] bool hideOnDeath = true;
    [Tooltip("Tamano de la nube de humo al morir (0 = sin nube).")]
    [SerializeField] float deathPoofScale = 1.2f;
    [Tooltip("Segundos (reales) entre que sale la nube y desaparece el sprite. Un poquito de espera hace que la nube lo 'tape'.")]
    [SerializeField, Min(0f)] float deathHideDelay = 0.05f;
    [Tooltip("Nube de polvo (sin estrellas) al REAPARECER tras morir. No sale al empezar la partida. 0 = nada.")]
    [SerializeField] float respawnPoofScale = 1.2f;

    PlayerAttack attack;
    PlayerAnimator anim;
    Animator visualAnimator;
    Color baseColor = Color.white;
    SpriteFlasher flasher;
    float invulnerableUntil;
    bool blinking;
    bool captured;
    readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    Coroutine hideRoutine;

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
        Sfx.Play(SfxId.PlayerHit, transform.position);
        Sfx.Play(SfxId.PlayerDeath, transform.position);
        if (deathPoofScale > 0f) Vfx.Play(VfxKind.Poof, (Vector2)transform.position + hitVfxOffset, Vector2.up, deathPoofScale);

        if (tintOnDeath && !hideOnDeath && visualRenderer != null) visualRenderer.color = deathColor;
        if (hideOnDeath)
        {
            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideAfter(deathHideDelay));
        }
        if (anim != null) anim.SetDead(true);
        SetAnimatorUnscaled(true);   // la animacion de muerte sigue durante el tiempo congelado

        if (GameManager.Instance != null) GameManager.Instance.OnPlayerDied(dir);
    }

    public void Revive(Vector3 position, float invulnerableSeconds = 0f)
    {
        bool wasDead = IsDead;
        IsDead = false;
        captured = false;
        invulnerableUntil = 0f;
        GrantInvulnerability(invulnerableSeconds);
        ShowRenderers();
        if (visualRenderer != null) visualRenderer.color = baseColor;
        if (anim != null) anim.SetDead(false);
        SetAnimatorUnscaled(false);
        if (attack != null) attack.CancelAttack();
        Controller.TeleportTo(position);
        Controller.InputEnabled = true;
        if (wasDead && respawnPoofScale > 0f)
            Vfx.Play(VfxKind.SpawnPoof, (Vector2)position + hitVfxOffset, Vector2.up, respawnPoofScale);
        if (wasDead) Sfx.Play(SfxId.Respawn, position);
    }

    // Oculta todo lo que se ve del jugador (sprite, sombra, espada...) y recuerda que estaba visible para devolverlo igual
    IEnumerator HideAfter(float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        hideRoutine = null;
        if (!IsDead) yield break;
        HideNow();
    }

    void HideNow()
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer) continue;
            r.enabled = false;
            hiddenRenderers.Add(r);
        }
    }

    /// Oculta al jugador (sprite, sombra, espada) sin matarlo. Lo usa el saco que cae cuando se acaba el tiempo.
    public void HideVisuals()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }
        HideNow();
    }

    /// Vuelve a mostrar lo que se oculto con HideVisuals.
    public void ShowVisuals() => ShowRenderers();

    void ShowRenderers()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }
        foreach (var r in hiddenRenderers)
            if (r != null) r.enabled = true;
        hiddenRenderers.Clear();
    }

    void SetAnimatorUnscaled(bool unscaled)
    {
        if (visualAnimator != null)
            visualAnimator.updateMode = unscaled ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
    }
}
