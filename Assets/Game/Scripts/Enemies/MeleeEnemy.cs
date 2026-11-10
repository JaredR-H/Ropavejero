using UnityEngine;

public class MeleeEnemy : Enemy
{
    enum State { Chasing, Charging, Striking, Recovering }

    [Header("Ataque")]
    [Tooltip("Distancia al jugador a la que empieza a cargar el ataque.")]
    [SerializeField] float attackDistance = 1.8f;
    [Tooltip("Tiempo de carga. Corto = poco tiempo para reaccionar.")]
    [SerializeField] float chargeTime = 0.3f;
    [SerializeField] float strikeTime = 0.22f;
    [Tooltip("Tiempo quieto despues del golpe.")]
    [SerializeField] float recoverTime = 0.45f;
    [Tooltip("Velocidad del empujon hacia el jugador al golpear.")]
    [SerializeField] float lungeSpeed = 6f;
    [Tooltip("En que punto del golpe (0 a 1) hace dano.")]
    [SerializeField, Range(0f, 1f)] float hitMoment = 0.35f;
    [Tooltip("Centro del golpe: distancia desde el enemigo hacia el jugador.")]
    [SerializeField] float attackReach = 1.1f;
    [SerializeField] float attackRadius = 0.85f;
    [SerializeField] bool alsoKillsOnContact = true;

    [Header("Animacion")]
    [Tooltip("La espada (pivote con EnemyWeaponVisual). Opcional.")]
    [SerializeField] EnemyWeaponVisual sword;
    [Tooltip("Vibracion de la carga del cuerpo (menor que en la bomba y el arquero).")]
    [SerializeField] float chargeIntensity = 0.6f;
    [SerializeField] float strikeIntensity = 1f;

    State state = State.Chasing;
    float stateTimer;
    float strikeElapsed;
    bool hitDone;
    Vector2 aimDir = Vector2.right;

    protected override bool ContactKills => alsoKillsOnContact;

    protected override void OnReset()
    {
        state = State.Chasing;
        if (sword != null) sword.CancelAttack();
    }

    protected override void OnDamaged()
    {
        // Un golpe interrumpe el ataque.
        if (state == State.Charging || state == State.Striking)
        {
            if (juice != null)
            {
                juice.Stop("LeanBack");
                juice.Stop("LeanForward");
            }
            if (sword != null) sword.CancelAttack();
            state = State.Recovering;
            stateTimer = recoverTime * 0.6f;
        }
    }

    protected override void Tick(float dt)
    {
        switch (state)
        {
            case State.Chasing:
                if (Vector2.Distance(PlayerPosition, rb.position) <= attackDistance) BeginCharge();
                break;

            case State.Charging:
                aimDir = DirectionToPlayer();               // sigue al jugador mientras carga
                FaceDirection(aimDir);
                stateTimer -= dt;
                if (stateTimer <= 0f) BeginStrike();
                break;

            case State.Striking:
                stateTimer -= dt;
                strikeElapsed += dt;
                if (!hitDone && strikeElapsed >= strikeTime * hitMoment) DoHit();
                if (stateTimer <= 0f)
                {
                    state = State.Recovering;
                    stateTimer = recoverTime;
                }
                break;

            case State.Recovering:
                stateTimer -= dt;
                if (stateTimer <= 0f) state = State.Chasing;
                break;
        }
    }

    protected override Vector2 ComputeDesiredVelocity()
    {
        switch (state)
        {
            case State.Chasing:
                Vector2 toPlayer = PlayerPosition - rb.position;
                if (toPlayer.sqrMagnitude < 0.0001f) return Vector2.zero;
                FaceDirection(toPlayer);
                return toPlayer.normalized * moveSpeed;

            case State.Striking:
                // Empujon corto hacia el jugador durante la primera parte del golpe
                return strikeElapsed < strikeTime * 0.6f ? aimDir * lungeSpeed : Vector2.zero;

            default:
                return Vector2.zero;     // cargando o recuperandose: quieto
        }
    }

    Vector2 DirectionToPlayer()
    {
        Vector2 d = PlayerCenter - rb.position;
        return d.sqrMagnitude > 0.0001f ? d.normalized : aimDir;
    }

    void BeginCharge()
    {
        state = State.Charging;
        stateTimer = chargeTime;
        aimDir = DirectionToPlayer();

        if (juice != null) juice.Play("LeanBack", aimDir, chargeTime, chargeIntensity);
        if (sword != null) sword.Charge(chargeTime);
    }

    void BeginStrike()
    {
        state = State.Striking;
        stateTimer = strikeTime;
        strikeElapsed = 0f;
        hitDone = false;
        aimDir = DirectionToPlayer();

        if (juice != null) juice.Play("LeanForward", aimDir, strikeTime, strikeIntensity);
        if (sword != null) sword.Strike(strikeTime);
    }

    void DoHit()
    {
        hitDone = true;
        Vector2 center = rb.position + aimDir * attackReach;
        foreach (var col in Physics2D.OverlapCircleAll(center, attackRadius))
            HazardUtil.TryKillPlayer(col, rb.position, false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackDistance);
        Gizmos.color = Color.red;
        Vector2 dir = Application.isPlaying ? aimDir : Vector2.right;
        Gizmos.DrawWireSphere((Vector2)transform.position + dir * attackReach, attackRadius);
    }
}
