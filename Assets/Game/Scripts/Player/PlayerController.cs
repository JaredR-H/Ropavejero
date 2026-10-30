using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum FacingDirection { Down, Up, Left, Right }

/// Movimiento top-down + dash + salto falso + empuje (knockback) y orientacion del sprite.
/// Controles: WASD / flechas = mover, Espacio = dash, C = salto falso, mouse = hacia donde mira y ataca.
/// Si el hijo "Visual" tiene un Animator, las animaciones eligen el sprite y este script solo voltea (flipX).
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] float moveSpeed = 5f;

    [Header("Dash (Espacio)")]
    [SerializeField] float dashSpeed = 16f;
    [SerializeField] float dashDuration = 0.14f;
    [SerializeField] float dashCooldown = 0.45f;
    [Tooltip("Marcado: durante el dash no te matan. Desmarcado: el dash solo te mueve rapido.")]
    [SerializeField] bool dashIsInvulnerable = false;
    [Tooltip("Opcional: TrailRenderer que solo emite durante el dash.")]
    [SerializeField] TrailRenderer dashTrail;

    [Header("Salto falso (C) (solo visual + inmune a trampas del suelo)")]
    [SerializeField] float jumpDuration = 0.45f;
    [SerializeField] float jumpHeight = 0.9f;
    [SerializeField] float airSpeedMultiplier = 1.15f;

    [Header("Orientacion")]
    [Tooltip("Marcado: el personaje mira hacia el mouse. Desmarcado: mira hacia donde camina.")]
    [SerializeField] bool facingFollowsMouse = true;

    [Header("Empuje (knockback)")]
    [Tooltip("Cuanto tarda en frenarse un empuje (unidades/segundo al cuadrado).")]
    [SerializeField] float knockbackDecay = 28f;

    [Header("Visual")]
    [SerializeField] Transform visual;              // hijo "Visual": es lo que sube al saltar
    [SerializeField] SpriteRenderer visualRenderer;
    [SerializeField] Transform shadow;              // hijo "Shadow": se queda en el suelo
    [Tooltip("Opcional: JuiceAnimator (squash & stretch y balanceo). Si esta vacio se busca en los hijos.")]
    [SerializeField] JuiceAnimator juice;

    [Header("Sprites estaticos (solo se usan si NO hay Animator)")]
    [SerializeField] Sprite spriteFront;
    [SerializeField] Sprite spriteBack;
    [SerializeField] Sprite spriteSide;             // dibujado mirando a la DERECHA (se voltea con flipX)

    Rigidbody2D rb;
    PlayerAim aim;
    Vector2 moveInput;
    Vector2 knockVelocity;
    Vector2 dashDir;
    float dashTimeLeft;
    float dashCooldownTimer;
    bool inputEnabled = true;
    bool usesAnimator;
    Vector3 visualBasePos;
    Vector3 shadowBaseScale = Vector3.one;
    Coroutine jumpRoutine;

    public FacingDirection Facing { get; private set; } = FacingDirection.Down;
    public bool IsAirborne { get; private set; }
    public bool IsDashing { get; private set; }
    public bool IsInvulnerable => IsDashing && dashIsInvulnerable;
    public float SpeedMultiplier { get; set; } = 1f;
    public Vector2 LastMoveDirection { get; private set; } = Vector2.down;

    /// true si el jugador realmente se esta desplazando (no cuenta empujar contra una pared).
    public bool IsMoving => moveInput.sqrMagnitude > 0.01f && rb != null && rb.linearVelocity.sqrMagnitude > 0.05f;

    /// Valor del parametro "Direction" del Animator: 0 = frente, 1 = espalda, 2 = perfil (izquierda y derecha).
    public int AnimDirection
    {
        get
        {
            switch (Facing)
            {
                case FacingDirection.Up: return 1;
                case FacingDirection.Left:
                case FacingDirection.Right: return 2;
                default: return 0;
            }
        }
    }

    public Vector2 FacingVector
    {
        get
        {
            switch (Facing)
            {
                case FacingDirection.Up: return Vector2.up;
                case FacingDirection.Left: return Vector2.left;
                case FacingDirection.Right: return Vector2.right;
                default: return Vector2.down;
            }
        }
    }

    public bool InputEnabled
    {
        get => inputEnabled;
        set
        {
            inputEnabled = value;
            if (!value)
            {
                moveInput = Vector2.zero;
                EndDash();
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        aim = GetComponent<PlayerAim>();
        if (visual != null) visualBasePos = visual.localPosition;
        if (shadow != null) shadowBaseScale = shadow.localScale;
        usesAnimator = GetComponentInChildren<Animator>(true) != null;
        if (juice == null) juice = GetComponentInChildren<JuiceAnimator>(true);
        if (dashTrail != null) dashTrail.emitting = false;
    }

    void Start() => ApplyFacingSprite();

    void Update()
    {
        if (juice != null) juice.SetMove(inputEnabled && (IsDashing || IsMoving) ? 1f : 0f);

        if (!inputEnabled) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
        float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
        moveInput = new Vector2(x, y).normalized;
        if (moveInput.sqrMagnitude > 0.01f) LastMoveDirection = moveInput;

        if (facingFollowsMouse && aim != null) UpdateFacing(aim.Direction);
        else UpdateFacing(moveInput);

        if (dashCooldownTimer > 0f) dashCooldownTimer -= Time.deltaTime;

        if (kb.spaceKey.wasPressedThisFrame) TryDash();

        if (kb.cKey.wasPressedThisFrame && !IsAirborne && !IsDashing)
            jumpRoutine = StartCoroutine(JumpRoutine());
    }

    void FixedUpdate()
    {
        Vector2 velocity;

        if (IsDashing)
        {
            velocity = dashDir * dashSpeed;
            dashTimeLeft -= Time.fixedDeltaTime;
            if (dashTimeLeft <= 0f) EndDash();
        }
        else
        {
            float speed = moveSpeed * SpeedMultiplier * (IsAirborne ? airSpeedMultiplier : 1f);
            velocity = moveInput * speed;
        }

        knockVelocity = Vector2.MoveTowards(knockVelocity, Vector2.zero, knockbackDecay * Time.fixedDeltaTime);
        rb.linearVelocity = velocity + knockVelocity;
    }

    // ---------- Dash ----------
    void TryDash()
    {
        if (IsDashing || dashCooldownTimer > 0f) return;

        Vector2 dir = moveInput.sqrMagnitude > 0.01f ? moveInput : (aim != null ? aim.Direction : FacingVector);
        dashDir = dir.normalized;
        dashTimeLeft = dashDuration;
        dashCooldownTimer = dashCooldown;
        IsDashing = true;
        if (dashTrail != null) dashTrail.emitting = true;
        if (juice != null) juice.Play("Dash", dashDir);     // estiramiento pronunciado en la direccion del dash
    }

    void EndDash()
    {
        IsDashing = false;
        if (dashTrail != null) dashTrail.emitting = false;
    }

    // ---------- Empuje ----------
    /// Empuja al jugador en la direccion dada. El empuje se frena solo (Knockback Decay).
    public void ApplyKnockback(Vector2 direction, float force)
    {
        EndDash();
        knockVelocity = direction.normalized * force;
    }

    // ---------- Orientacion ----------
    void UpdateFacing(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.01f) return;

        FacingDirection newFacing;
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            newFacing = dir.x > 0f ? FacingDirection.Right : FacingDirection.Left;
        else
            newFacing = dir.y > 0f ? FacingDirection.Up : FacingDirection.Down;

        if (newFacing == Facing) return;
        Facing = newFacing;
        ApplyFacingSprite();
    }

    void ApplyFacingSprite()
    {
        if (visualRenderer == null) return;

        // El volteo siempre lo hace este script: las animaciones solo cambian el sprite.
        visualRenderer.flipX = Facing == FacingDirection.Left;
        if (usesAnimator) return;

        switch (Facing)
        {
            case FacingDirection.Down:
                if (spriteFront != null) visualRenderer.sprite = spriteFront;
                break;
            case FacingDirection.Up:
                if (spriteBack != null) visualRenderer.sprite = spriteBack;
                break;
            default:   // Left y Right
                if (spriteSide != null) visualRenderer.sprite = spriteSide;
                break;
        }
    }

    // ---------- Salto falso ----------
    IEnumerator JumpRoutine()
    {
        IsAirborne = true;
        float t = 0f;
        while (t < jumpDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / jumpDuration);
            float k = 4f * n * (1f - n);                       // parabola 0 -> 1 -> 0
            if (visual != null) visual.localPosition = visualBasePos + Vector3.up * (k * jumpHeight);
            if (shadow != null) shadow.localScale = shadowBaseScale * (1f - 0.3f * k);
            yield return null;
        }
        ResetVisualOffsets();
        IsAirborne = false;
        jumpRoutine = null;
    }

    void ResetVisualOffsets()
    {
        if (visual != null) visual.localPosition = visualBasePos;
        if (shadow != null) shadow.localScale = shadowBaseScale;
    }

    // ---------- Reaparicion ----------
    public void TeleportTo(Vector2 position)
    {
        if (jumpRoutine != null) StopCoroutine(jumpRoutine);
        jumpRoutine = null;
        IsAirborne = false;
        EndDash();
        knockVelocity = Vector2.zero;
        ResetVisualOffsets();
        if (juice != null) juice.StopAll();

        rb.linearVelocity = Vector2.zero;
        rb.position = position;
        transform.position = position;

        Facing = FacingDirection.Down;
        ApplyFacingSprite();
    }
}
