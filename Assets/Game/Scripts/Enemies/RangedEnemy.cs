using UnityEngine;

public class RangedEnemy : Enemy
{
    enum State { Moving, Aiming, Recovering }

    [Header("Distancia")]
    [Tooltip("Mas cerca que esto: se aleja.")]
    [SerializeField] float fleeDistance = 4f;
    [Tooltip("Distancia comoda para rodear al jugador.")]
    [SerializeField] float preferredDistance = 6f;
    [SerializeField] float distanceTolerance = 1.5f;
    [Tooltip("Cada cuantos segundos cambia de sentido al rodear (al azar entre los dos valores).")]
    [SerializeField] Vector2 strafeFlipTime = new Vector2(2f, 4f);

    [Header("Paredes (nunca se pega a ellas)")]
    [Tooltip("Capa de las paredes (Walls). Sirve para no pegarse a ellas y para no disparar a traves de ellas.")]
    [SerializeField] LayerMask wallMask;
    [Tooltip("Distancia minima que intenta mantener respecto a cualquier pared.")]
    [SerializeField] float wallMargin = 0.8f;
    [Tooltip("Hasta donde 'mira' en cada direccion para buscar espacio libre.")]
    [SerializeField] float lookDistance = 3.5f;
    [SerializeField, Range(8, 32)] int rayCount = 16;
    [Tooltip("Cuanto prefiere las zonas amplias y el centro de la sala cuando esta cerca de una pared.")]
    [SerializeField] float openSpaceWeight = 1.2f;
    [Tooltip("Que tan rapido gira su direccion (menor = mas suave).")]
    [SerializeField] float turnSpeed = 8f;

    [Header("Disparo")]
    [SerializeField] EnemyProjectile projectilePrefab;
    [Tooltip("Opcional: de donde sale el proyectil. Si esta vacio, del centro del enemigo.")]
    [SerializeField] Transform muzzle;
    [SerializeField] float projectileSpeed = 7f;
    [Tooltip("Segundos entre disparos (al azar entre los dos valores).")]
    [SerializeField] Vector2 fireInterval = new Vector2(2f, 3.5f);
    [Tooltip("Tiempo quieto apuntando antes de disparar (aviso para el jugador).")]
    [SerializeField] float aimTime = 0.5f;
    [Tooltip("Tiempo quieto despues de disparar: la oportunidad del jugador para acercarse.")]
    [SerializeField] float recoverTime = 0.9f;

    [Header("Aviso al apuntar")]
    [Tooltip("Arco que lo sigue apuntando al jugador (se tensa al apuntar y se aplasta al disparar). Opcional.")]
    [SerializeField] EnemyWeaponVisual bow;
    [Tooltip("Que tan marcada es la animacion de carga del cuerpo mientras apunta.")]
    [SerializeField] float chargeIntensity = 1f;
    [Tooltip("Ademas de la animacion de carga, parpadea en blanco mientras apunta.")]
    [SerializeField] bool blinkWhileAiming = false;
    [SerializeField] float blinkInterval = 0.07f;

    State state = State.Moving;
    float stateTimer;
    float fireTimer;
    float flipTimer;
    float strafeSign = 1f;
    float radius = 0.3f;
    Vector2 lastDir;
    Room room;
    Vector2[] rayDirs;
    float[] free;

    Vector2 Origin => muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;

    protected override void Awake()
    {
        base.Awake();
        if (body != null) radius = Mathf.Max(0.1f, body.bounds.extents.x * 0.9f);
        room = GetComponentInParent<Room>();
        EnsureRays();
        OnReset();
    }

    void EnsureRays()
    {
        if (rayDirs != null && rayDirs.Length == rayCount) return;

        rayDirs = new Vector2[rayCount];
        free = new float[rayCount];
        for (int i = 0; i < rayCount; i++)
        {
            float a = (360f / rayCount) * i * Mathf.Deg2Rad;
            rayDirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
    }

    protected override void OnReset()
    {
        state = State.Moving;
        lastDir = Vector2.zero;
        strafeSign = Random.value < 0.5f ? 1f : -1f;
        flipTimer = Random.Range(strafeFlipTime.x, strafeFlipTime.y);
        ScheduleNextShot();
        if (bow != null) bow.Cancel();
    }

    protected override void OnDamaged()
    {
        // Un golpe interrumpe el disparo.
        if (state == State.Aiming)
        {
            if (flasher != null) flasher.Cancel();
            if (juice != null) juice.Stop("Charge");
            if (bow != null) bow.Cancel();
            state = State.Moving;
            ScheduleNextShot();
        }
    }

    protected override void Tick(float dt)
    {
        flipTimer -= dt;
        if (flipTimer <= 0f)
        {
            strafeSign = -strafeSign;
            flipTimer = Random.Range(strafeFlipTime.x, strafeFlipTime.y);
        }

        switch (state)
        {
            case State.Moving:
                fireTimer -= dt;
                if (fireTimer <= 0f && HasLineOfFire()) BeginAim();
                break;

            case State.Aiming:
                stateTimer -= dt;
                if (stateTimer <= 0f) Shoot();
                break;

            case State.Recovering:
                stateTimer -= dt;
                if (stateTimer <= 0f)
                {
                    state = State.Moving;
                    ScheduleNextShot();
                }
                break;
        }
    }

    protected override Vector2 ComputeDesiredVelocity()
    {
        Vector2 toPlayer = PlayerPosition - rb.position;
        FaceDirection(toPlayer);

        // Quieto mientras apunta y despues de disparar
        if (state != State.Moving)
        {
            lastDir = Vector2.zero;
            return Vector2.zero;
        }

        Vector2 desired = BehaviourDirection(toPlayer);
        if (desired == Vector2.zero) return Vector2.zero;

        Vector2 steered = wallMask.value != 0 ? SteerAwayFromWalls(desired) : desired;
        return steered * moveSpeed;
    }

    /// Lo que "quiere" hacer: alejarse, acercarse despacio o rodear al jugador.
    Vector2 BehaviourDirection(Vector2 toPlayer)
    {
        float dist = toPlayer.magnitude;
        Vector2 dir = dist > 0.001f ? toPlayer / dist : Vector2.down;
        Vector2 tangent = new Vector2(-dir.y, dir.x) * strafeSign;

        Vector2 move;
        if (dist < fleeDistance) move = -dir + tangent * 0.6f;                                      // se aleja rodeando
        else if (dist > preferredDistance + distanceTolerance) move = dir * 0.6f + tangent * 0.6f;  // se acerca despacio
        else move = tangent;                                                                        // rodea

        return move.sqrMagnitude > 0.0001f ? move.normalized : Vector2.zero;
    }

    /// Abanico de rayos: cada direccion se puntua por lo que se parece a lo que quiere hacer, por cuanto espacio libre
    /// tiene delante y, cuando esta cerca de un muro, por lo que se acerca al centro de la sala. Las direcciones que
    /// terminan pegadas a una pared se penalizan, asi nunca se acorrala ni roza los muros.
    Vector2 SteerAwayFromWalls(Vector2 desired)
    {
        EnsureRays();
        Vector2 pos = rb.position;

        float minFree = lookDistance;
        for (int i = 0; i < rayCount; i++)
        {
            RaycastHit2D hit = Physics2D.Raycast(pos, rayDirs[i], lookDistance, wallMask);
            float f = hit.collider != null ? Mathf.Max(0f, hit.distance - radius) : lookDistance;
            free[i] = f;
            if (f < minFree) minFree = f;
        }

        // 0 = lejos de todo, 1 = pegado a una pared
        float tight = 1f - Mathf.Clamp01(minFree / (wallMargin * 2f));

        Vector2 toCenter = Vector2.zero;
        if (room != null)
        {
            Vector2 c = (Vector2)room.CameraPosition - pos;
            if (c.sqrMagnitude > 0.01f) toCenter = c.normalized;
        }

        float bestScore = float.NegativeInfinity;
        Vector2 best = desired;
        for (int i = 0; i < rayCount; i++)
        {
            Vector2 d = rayDirs[i];
            float f = free[i];

            float score = Vector2.Dot(d, desired)
                        + (f / lookDistance) * openSpaceWeight * (0.3f + tight)
                        + Vector2.Dot(d, toCenter) * openSpaceWeight * tight
                        + Vector2.Dot(d, lastDir) * 0.15f;                    // un poco de inercia: evita temblar

            if (f < wallMargin) score -= (wallMargin - f) * 3f;               // demasiado cerca de la pared
            if (f < wallMargin * 0.5f) score -= 3f;                           // casi tocandola

            if (score > bestScore)
            {
                bestScore = score;
                best = d;
            }
        }

        if (lastDir.sqrMagnitude < 0.01f) lastDir = best;
        else lastDir = Vector2.Lerp(lastDir, best, Mathf.Clamp01(turnSpeed * Time.fixedDeltaTime)).normalized;
        return lastDir;
    }

    bool HasLineOfFire()
    {
        if (wallMask.value == 0) return true;
        return Physics2D.Linecast(Origin, PlayerCenter, wallMask).collider == null;
    }

    void ScheduleNextShot() => fireTimer = Random.Range(fireInterval.x, fireInterval.y);

    void BeginAim()
    {
        state = State.Aiming;
        stateTimer = aimTime;

        // Aviso: el cuerpo carga (balanceo + squash & stretch) y el arco se tensa
        if (juice != null) juice.Play("Charge", Vector2.zero, aimTime, chargeIntensity);
        if (bow != null) bow.Draw(aimTime);
        if (blinkWhileAiming && flasher != null) flasher.Blink(aimTime, blinkInterval);
    }

    void Shoot()
    {
        if (flasher != null) flasher.Cancel();
        if (juice != null) juice.Play("Release");
        if (bow != null) bow.Release();

        if (projectilePrefab != null && PlayerAlive)
        {
            Vector2 origin = Origin;
            Vector2 dir = PlayerCenter - origin;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();

            EnemyProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity,
                                                     room != null ? room.transform : null);
            projectile.Launch(dir, projectileSpeed);
            Track(projectile.gameObject);
        }

        state = State.Recovering;
        stateTimer = recoverTime;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, fleeDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, preferredDistance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, wallMargin);
    }
}
