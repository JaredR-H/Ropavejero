using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(Rigidbody2D))]
public abstract class Enemy : MonoBehaviour, IHittable, IResettable
{
    [Header("Vida")]
    [SerializeField, Min(1)] protected int maxHealth = 3;

    [Header("Movimiento")]
    [SerializeField, Min(0f)] protected float moveSpeed = 2.5f;
    [Tooltip("Segundos quieto al entrar a la sala o al reaparecer, antes de empezar a actuar.")]
    [SerializeField, Min(0f)] protected float wakeDelay = 0.6f;

    [Header("Al recibir un golpe")]
    [Tooltip("Fuerza del empuje. La distancia empujada es aprox. fuerza al cuadrado / (2 x decay).")]
    [SerializeField] protected float knockbackForce = 9f;
    [SerializeField] protected float knockbackDecay = 32f;
    [Tooltip("Segundos aturdido (sin actuar) tras un golpe.")]
    [SerializeField, Min(0f)] protected float hitStun = 0.18f;

    [Header("Contacto")]
    [Tooltip("Marcado: mientras despierta (antes de moverse) no mata.")]
    [SerializeField] protected bool harmlessWhileWaking = true;

    [Header("Visual (opcional)")]
    [SerializeField] protected SpriteRenderer sr;
    [SerializeField] protected SpriteFlasher flasher;
    [Tooltip("Opcional: JuiceAnimator del Visual (squash & stretch y balanceo al caminar y en reposo).")]
    [SerializeField] protected JuiceAnimator juice;
    [Tooltip("Voltea el sprite segun hacia donde mira. Dibuja el sprite mirando a la DERECHA.")]
    [SerializeField] protected bool faceMoveDirection = true;
    [Tooltip("Prefab opcional que aparece al ser derrotado (debe destruirse solo).")]
    [SerializeField] protected GameObject defeatEffect;

    protected Rigidbody2D rb;
    protected Collider2D body;
    protected int health;
    protected bool defeated;
    protected float wakeTimer;
    protected float stunTimer;
    protected Vector2 knockVelocity;

    PlayerHealth player;
    Collider2D playerCollider;
    Vector3 startLocalPos;
    readonly List<GameObject> projectiles = new List<GameObject>();

    protected bool GameIsPlaying => GameManager.Instance == null || GameManager.Instance.IsPlaying;
    protected bool IsWaking => wakeTimer > 0f;
    protected virtual bool ContactKills => true;

    protected PlayerHealth Player
    {
        get
        {
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerHealth>();
                if (player != null) playerCollider = player.GetComponent<Collider2D>();
            }
            return player;
        }
    }

    protected bool PlayerAlive => Player != null && !Player.IsDead;
    protected Vector2 PlayerPosition => Player != null ? (Vector2)Player.transform.position : rb.position;
    protected Vector2 PlayerCenter => playerCollider != null ? (Vector2)playerCollider.bounds.center : PlayerPosition;

    protected bool CanAct => GameIsPlaying && !defeated && !IsWaking && stunTimer <= 0f && PlayerAlive;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        body = GetComponent<Collider2D>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        if (flasher == null) flasher = GetComponentInChildren<SpriteFlasher>();
        if (juice == null) juice = GetComponentInChildren<JuiceAnimator>(true);

        startLocalPos = transform.localPosition;
        health = maxHealth;
        wakeTimer = wakeDelay;
    }

    protected virtual void Update()
    {
        if (defeated || !GameIsPlaying) return;

        float dt = Time.deltaTime;
        if (wakeTimer > 0f) wakeTimer = Mathf.Max(0f, wakeTimer - dt);
        if (stunTimer > 0f) stunTimer = Mathf.Max(0f, stunTimer - dt);

        if (CanAct) Tick(dt);
    }

    protected virtual void FixedUpdate()
    {
        Vector2 desired = CanAct ? ComputeDesiredVelocity() : Vector2.zero;
        if (juice != null) juice.SetMove(desired.sqrMagnitude > 0.01f ? 1f : 0f);
        knockVelocity = Vector2.MoveTowards(knockVelocity, Vector2.zero, knockbackDecay * Time.fixedDeltaTime);
        rb.linearVelocity = desired + knockVelocity;
    }

    /// Logica por frame (temporizadores, disparos...). Solo se llama cuando el enemigo puede actuar.
    protected virtual void Tick(float dt) { }

    /// Hacia donde quiere moverse (velocidad). Solo se llama cuando el enemigo puede actuar.
    protected abstract Vector2 ComputeDesiredVelocity();

    protected void FaceDirection(Vector2 dir)
    {
        if (!faceMoveDirection || sr == null) return;
        if (Mathf.Abs(dir.x) > 0.05f) sr.flipX = dir.x < 0f;
    }

    // ---------- Recibir golpes ----------
    public void OnHit(PlayerAttack attacker)
    {
        if (defeated) return;

        Vector2 from = attacker != null ? (Vector2)attacker.transform.position : rb.position + Vector2.down;
        Vector2 dir = rb.position - from;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        TakeHit(1, dir.normalized);
    }

    protected virtual void TakeHit(int damage, Vector2 direction)
    {
        health -= damage;
        knockVelocity = direction * knockbackForce;
        stunTimer = hitStun;

        if (flasher != null) flasher.Flash(0.09f);
        if (juice != null) juice.Play("Hit");
        if (GameFeel.Instance != null) GameFeel.Instance.EnemyHit(direction);

        OnDamaged();
        if (health <= 0) Defeat(direction);
    }

    /// Se llama en cada golpe recibido (por ejemplo, para cancelar un disparo).
    protected virtual void OnDamaged() { }

    protected virtual void Defeat(Vector2 direction)
    {
        defeated = true;
        ClearProjectiles();
        if (defeatEffect != null) Destroy(Instantiate(defeatEffect, transform.position, Quaternion.identity), 2f);
        gameObject.SetActive(false);
    }

    // ---------- Contacto ----------
    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
        if (!ContactKills || defeated) return;
        if (IsWaking && harmlessWhileWaking) return;
        HazardUtil.TryKillPlayer(collision.collider, transform.position, false);
    }

    // ---------- Proyectiles propios (se borran al reiniciar la sala) ----------
    protected void Track(GameObject projectile) => projectiles.Add(projectile);

    protected void ClearProjectiles()
    {
        foreach (var p in projectiles)
            if (p != null) Destroy(p);
        projectiles.Clear();
    }

    // ---------- Reinicio con la sala ----------
    public void ResetState()
    {
        StopAllCoroutines();
        if (flasher != null) flasher.Cancel();
        if (juice != null) juice.StopAll();
        ClearProjectiles();

        defeated = false;
        health = maxHealth;
        wakeTimer = wakeDelay;
        stunTimer = 0f;
        knockVelocity = Vector2.zero;

        gameObject.SetActive(true);
        transform.localPosition = startLocalPos;
        if (rb != null)
        {
            rb.position = transform.position;
            rb.linearVelocity = Vector2.zero;
        }
        OnReset();
    }

    protected virtual void OnReset() { }
}
