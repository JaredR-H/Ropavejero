using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public abstract class Enemy : MonoBehaviour, IHittable, IResettable
{
    [Header("Vida")]
    [SerializeField, Min(1)] protected int maxHealth = 3;

    [Header("Movimiento")]
    [SerializeField, Min(0f)] protected float moveSpeed = 2.5f;
    [Tooltip("Segundos para llegar a su velocidad desde quieto (arranque suave). 0 = instantaneo.")]
    [SerializeField, Min(0f)] protected float accelerationTime = 0.12f;
    [Tooltip("Segundos para frenar hasta quedar quieto (frenado suave). 0 = instantaneo.")]
    [SerializeField, Min(0f)] protected float decelerationTime = 0.15f;
    [Tooltip("Segundos quieto al entrar a la sala o al reaparecer, antes de empezar a actuar.")]
    [SerializeField, Min(0f)] protected float wakeDelay = 0.6f;

    [Header("Al recibir un golpe")]
    [Tooltip("Fuerza del empuje. La distancia empujada es aprox. fuerza al cuadrado / (2 x decay).")]
    [SerializeField] protected float knockbackForce = 9f;
    [SerializeField] protected float knockbackDecay = 32f;
    [Tooltip("Segundos aturdido (sin actuar) tras un golpe.")]
    [SerializeField, Min(0f)] protected float hitStun = 0.18f;
    [Tooltip("Multiplica el hitstop y el shake de camara al golpear a este enemigo (1 = lo que diga GameFeel).")]
    [SerializeField, Min(0f)] protected float hitFeelBoost = 1.3f;
    [Tooltip("Segundos que tarda en desaparecer al ser derrotado (se ve el destello blanco del ultimo golpe).")]
    [SerializeField, Min(0f)] protected float defeatHideDelay = 0.1f;
    [Tooltip("Tamano de las particulas al ser golpeado.")]
    [SerializeField] protected float hitVfxScale = 1f;
    [Tooltip("Sonido al recibir un golpe.")]
    [SerializeField] protected SfxId hitSound = SfxId.EnemyHit;
    [Tooltip("Sonido al ser derrotado.")]
    [SerializeField] protected SfxId defeatSound = SfxId.EnemyDeath;
    [Tooltip("Tamano de la nube grande al ser derrotado (1 = un poco mas grande que un personaje normal, 0 = sin nube).")]
    [SerializeField] protected float defeatPoofScale = 1.5f;
    [Tooltip("Duracion del destello blanco al ser golpeado.")]
    [SerializeField] protected float hitFlashTime = 0.1f;

    [Header("Navegacion (rodear rocas y paredes)")]
    [Tooltip("Capa de paredes y rocas (Walls). Si esta vacia, el enemigo camina en linea recta como antes.")]
    [SerializeField] protected LayerMask obstacleMask;
    [Tooltip("Hasta donde mira hacia adelante para ver si hay una roca en el camino.")]
    [SerializeField] protected float avoidLookAhead = 1.2f;
    [SerializeField, Range(8, 32)] protected int steerRays = 16;
    [Tooltip("Que tan rapido gira su direccion al esquivar (menor = mas suave).")]
    [SerializeField] protected float steerTurnSpeed = 10f;
    [Tooltip("Segundos casi sin avanzar (queriendo moverse) para considerarse atascado.")]
    [SerializeField] protected float stuckTime = 0.4f;
    [Tooltip("Segundos que rodea el obstaculo hacia un lado cuando se atasca.")]
    [SerializeField] protected float detourTime = 0.8f;

    [Header("Separacion entre enemigos")]
    [Tooltip("Los enemigos no chocan entre si (se atraviesan) y se empujan suavemente para no amontonarse.")]
    [SerializeField] protected bool ignoreOtherEnemies = true;
    [Tooltip("Distancia a la que empiezan a separarse de otro enemigo.")]
    [SerializeField] protected float separationRadius = 0.9f;
    [Tooltip("Fuerza de la separacion (respecto a su velocidad).")]
    [SerializeField] protected float separationWeight = 0.8f;

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
    [Tooltip("Marcalo si el dibujo del enemigo mira a la IZQUIERDA (como el Coyongo). Asi se voltea al reves.")]
    [SerializeField] protected bool artFacesLeft = false;
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

    static readonly List<Enemy> active = new List<Enemy>();
    protected float bodyRadius = 0.3f;
    Vector2 steerDir;
    Vector2 moveVelocity;
    Vector2 lastPos;
    float stuckTimer;
    float detourTimer;
    float detourSign = 1f;
    bool wantsToMove;
    Vector3 startLocalPos;
    readonly List<GameObject> projectiles = new List<GameObject>();

    /// Se dispara cuando el jugador derrota a un enemigo (por ejemplo, para recargar la barra del ataque cargado).
    public static event System.Action<Enemy> AnyDefeated;
    protected void NotifyDefeated() => AnyDefeated?.Invoke(this);

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
        if (body != null) bodyRadius = Mathf.Max(0.1f, Mathf.Min(body.bounds.extents.x, body.bounds.extents.y) * 0.9f);
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        if (flasher == null) flasher = GetComponentInChildren<SpriteFlasher>();
        // Sin SpriteFlasher el enemigo no parpadea en blanco al ser golpeado: se anade solo sobre su sprite
        if (flasher == null && sr != null) flasher = sr.gameObject.AddComponent<SpriteFlasher>();
        if (juice == null) juice = GetComponentInChildren<JuiceAnimator>(true);

        startLocalPos = transform.localPosition;
        health = maxHealth;
        wakeTimer = wakeDelay;
    }

    protected virtual void OnEnable()
    {
        // Los enemigos no se empujan fisicamente entre ellos (asi no se quedan pegados); la separacion la hace el codigo.
        if (ignoreOtherEnemies && body != null)
            foreach (var other in active)
                if (other != null && other.body != null && other != this)
                    Physics2D.IgnoreCollision(body, other.body, true);
        if (!active.Contains(this)) active.Add(this);
        if (rb != null) lastPos = rb.position;
        stuckTimer = 0f;
        detourTimer = 0f;
    }

    protected virtual void OnDisable() => active.Remove(this);

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
        float dt = Time.fixedDeltaTime;
        wantsToMove = false;
        Vector2 desired = CanAct ? ComputeDesiredVelocity() : Vector2.zero;
        if (desired.sqrMagnitude > 0.01f)
        {
            wantsToMove = true;
            desired += SeparationVelocity();
        }

        // Arranque y frenado suaves
        bool speedingUp = desired.sqrMagnitude > moveVelocity.sqrMagnitude;
        float time = speedingUp ? accelerationTime : decelerationTime;
        if (time <= 0f) moveVelocity = desired;
        else
        {
            float rate = Mathf.Max(desired.magnitude, moveVelocity.magnitude, 0.01f) / time;
            moveVelocity = Vector2.MoveTowards(moveVelocity, desired, rate * dt);
        }

        if (juice != null) juice.SetMove(moveVelocity.sqrMagnitude > 0.05f ? 1f : 0f);
        knockVelocity = Vector2.MoveTowards(knockVelocity, Vector2.zero, knockbackDecay * dt);
        rb.linearVelocity = moveVelocity + knockVelocity;

        UpdateStuck(dt);
    }

    // ---------- Navegacion ----------
    /// Direccion (normalizada) para ir hacia 'desiredDir' rodeando rocas y paredes. Multiplicala por la velocidad.
    protected Vector2 Navigate(Vector2 desiredDir)
    {
        if (desiredDir.sqrMagnitude < 0.0001f) return Vector2.zero;
        desiredDir.Normalize();
        Vector2 dir = desiredDir;

        if (obstacleMask.value != 0)
        {
            // Atascado: rodea el obstaculo hacia un lado durante un momento
            if (detourTimer > 0f)
                dir = (desiredDir + new Vector2(-desiredDir.y, desiredDir.x) * detourSign * 1.5f).normalized;

            if (IsBlocked(rb.position, dir)) dir = BestOpenDirection(rb.position, dir);
        }

        if (steerDir.sqrMagnitude < 0.01f) steerDir = dir;
        else steerDir = Vector2.Lerp(steerDir, dir, Mathf.Clamp01(steerTurnSpeed * Time.fixedDeltaTime)).normalized;
        return steerDir;
    }

    bool IsBlocked(Vector2 pos, Vector2 dir)
    {
        return Physics2D.CircleCast(pos, bodyRadius, dir, avoidLookAhead, obstacleMask).collider != null;
    }

    // Abanico de rayos: la mejor direccion es la que mas se parece a la deseada y tiene mas espacio libre.
    Vector2 BestOpenDirection(Vector2 pos, Vector2 desired)
    {
        float bestScore = float.NegativeInfinity;
        Vector2 best = desired;
        for (int i = 0; i < steerRays; i++)
        {
            float a = (360f / steerRays) * i * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            RaycastHit2D hit = Physics2D.CircleCast(pos, bodyRadius, d, avoidLookAhead, obstacleMask);
            float free = hit.collider != null ? hit.distance : avoidLookAhead;

            float score = Vector2.Dot(d, desired) + (free / avoidLookAhead) * 1.5f + Vector2.Dot(d, steerDir) * 0.2f;
            if (free < bodyRadius) score -= 3f;
            if (score > bestScore)
            {
                bestScore = score;
                best = d;
            }
        }
        return best;
    }

    // Empuje suave para que los enemigos no se amontonen en el mismo sitio
    Vector2 SeparationVelocity()
    {
        if (separationRadius <= 0f || separationWeight <= 0f) return Vector2.zero;
        Vector2 pos = rb.position;
        Vector2 push = Vector2.zero;
        foreach (var other in active)
        {
            if (other == this || other == null || other.defeated || other.rb == null) continue;
            Vector2 d = pos - other.rb.position;
            float dist = d.magnitude;
            if (dist >= separationRadius) continue;
            if (dist < 0.001f) d = Random.insideUnitCircle;
            push += d.normalized * (1f - dist / separationRadius);
        }
        return push * moveSpeed * separationWeight;
    }

    // Si quiere moverse pero casi no avanza, rodea el obstaculo hacia un lado (y cambia de lado si vuelve a atascarse)
    void UpdateStuck(float dt)
    {
        Vector2 pos = rb.position;
        float moved = (pos - lastPos).magnitude;
        lastPos = pos;

        if (detourTimer > 0f) detourTimer -= dt;

        if (!wantsToMove || knockVelocity.sqrMagnitude > 0.5f)
        {
            stuckTimer = 0f;
            return;
        }

        if (moved < moveSpeed * dt * 0.25f) stuckTimer += dt;
        else stuckTimer = Mathf.Max(0f, stuckTimer - dt);

        if (stuckTimer >= stuckTime)
        {
            stuckTimer = 0f;
            detourSign = detourTimer > 0f ? -detourSign : (Random.value < 0.5f ? 1f : -1f);
            detourTimer = detourTime;
        }
    }

    /// Logica por frame (temporizadores, disparos...). Solo se llama cuando el enemigo puede actuar.
    protected virtual void Tick(float dt) { }

    /// Hacia donde quiere moverse (velocidad). Solo se llama cuando el enemigo puede actuar.
    protected abstract Vector2 ComputeDesiredVelocity();

    protected void FaceDirection(Vector2 dir)
    {
        if (!faceMoveDirection || sr == null) return;
        if (Mathf.Abs(dir.x) > 0.05f) sr.flipX = artFacesLeft ? dir.x > 0f : dir.x < 0f;
    }

    // ---------- Recibir golpes ----------
    public void OnHit(PlayerAttack attacker)
    {
        if (defeated) return;

        Vector2 from = attacker != null ? (Vector2)attacker.transform.position : rb.position + Vector2.down;
        Vector2 dir = rb.position - from;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;

        // El ataque cargado trae mas dano, mas empuje y mas sensacion de golpe.
        int damage = attacker != null ? attacker.CurrentDamage : 1;
        float knockback = attacker != null ? attacker.KnockbackMultiplier : 1f;
        float feel = attacker != null ? attacker.HitFeelMultiplier : 1f;
        TakeHit(damage, dir.normalized, knockback, feel);
    }

    protected virtual void TakeHit(int damage, Vector2 direction, float knockbackMultiplier = 1f, float feelMultiplier = 1f)
    {
        health -= damage;
        knockVelocity = direction * knockbackForce * knockbackMultiplier;
        stunTimer = hitStun;

        if (flasher != null) flasher.Flash(hitFlashTime);
        if (juice != null) juice.Play("Hit", Vector2.zero, 0f, feelMultiplier > 1f ? 1.6f : 1f);
        if (GameFeel.Instance != null) GameFeel.Instance.EnemyHit(direction, feelMultiplier * hitFeelBoost);
        Vfx.Play(VfxKind.HitEnemy, body != null ? (Vector2)body.bounds.center : rb.position, direction,
                 hitVfxScale * (feelMultiplier > 1f ? 1.4f : 1f));
        if (health > 0) Sfx.Play(hitSound, rb.position, 1f, feelMultiplier > 1f ? 0.9f : 1f);

        OnDamaged();
        if (health <= 0) Defeat(direction);
    }

    /// Se llama en cada golpe recibido (por ejemplo, para cancelar un disparo).
    protected virtual void OnDamaged() { }

    protected virtual void Defeat(Vector2 direction)
    {
        defeated = true;
        NotifyDefeated();
        ClearProjectiles();
        if (defeatEffect != null) Destroy(Instantiate(defeatEffect, transform.position, Quaternion.identity), 2f);
        if (defeatPoofScale > 0f)
            Vfx.Play(VfxKind.Poof, body != null ? (Vector2)body.bounds.center : rb.position, Vector2.up, defeatPoofScale);
        Sfx.Play(hitSound, rb.position);
        Sfx.Play(defeatSound, rb.position);

        // Se queda un instante (con el destello blanco) antes de desaparecer
        if (body != null) body.enabled = false;
        moveVelocity = Vector2.zero;
        if (defeatHideDelay > 0f && isActiveAndEnabled) StartCoroutine(HideAfter(defeatHideDelay));
        else gameObject.SetActive(false);
    }

    System.Collections.IEnumerator HideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);      // con escala de tiempo: dura lo que dure el hitstop
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
        moveVelocity = Vector2.zero;
        steerDir = Vector2.zero;
        if (body != null) body.enabled = true;
        stuckTimer = 0f;
        detourTimer = 0f;

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
