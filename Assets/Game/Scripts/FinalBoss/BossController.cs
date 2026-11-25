using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossController : Enemy, IRoomObjective
{
    public static BossController Active { get; private set; }

    enum Attack { None, Melee, Ranged, Summon, Sack }
    enum Mode { Idle, Walk, Busy, Lunge, Stunned }

    [Header("Jefe")]
    [SerializeField] string bossName = "El Ropavejero";
    [Tooltip("Segundos quieto al empezar la pelea antes del primer movimiento.")]
    [SerializeField] float introDelay = 1.2f;
    [Tooltip("Marcado: tocar al jefe mata. Desmarcado: solo matan sus ataques (mas justo).")]
    [SerializeField] bool contactKills = false;
    [Tooltip("Empuje al recibir un golpe (sutil: no interrumpe sus ataques).")]
    [SerializeField] float hitKnockback = 1.5f;
    [Tooltip("Al caminar se detiene a esta distancia del jugador.")]
    [SerializeField] float stopDistance = 1.6f;

    [Header("Ritmo")]
    [Tooltip("Segundos caminando hacia el jugador entre un ataque y otro (al azar entre los dos).")]
    [SerializeField] Vector2 walkTime = new Vector2(0.8f, 1.6f);
    [Tooltip("Pausa despues de cada ataque.")]
    [SerializeField] float recoverTime = 0.5f;
    [Tooltip("Con esta fraccion de vida o menos se enoja: ataca mas rapido y repite mas.")]
    [SerializeField, Range(0f, 1f)] float enrageAt = 0.5f;
    [Tooltip("Multiplica los tiempos de carga y espera cuando esta enojado (0,75 = 25% mas rapido).")]
    [SerializeField, Range(0.3f, 1f)] float enragedPace = 0.75f;
    [SerializeField] int enragedExtraRepeats = 1;

    [Header("Eleccion de ataque segun la distancia")]
    [Tooltip("Menos que esto = CERCA.")]
    [SerializeField] float nearDistance = 3f;
    [Tooltip("Mas que esto = LEJOS. Entre las dos = MEDIA.")]
    [SerializeField] float farDistance = 6.5f;
    [Tooltip("Peso de cada ataque: X = cerca, Y = media, Z = lejos. Mayor = mas probable. 0 = nunca.")]
    [SerializeField] Vector3 meleeWeights = new Vector3(6f, 1f, 0f);
    [SerializeField] Vector3 rangedWeights = new Vector3(1f, 3f, 4f);
    [SerializeField] Vector3 sackWeights = new Vector3(1f, 3f, 2f);
    [SerializeField] Vector3 summonWeights = new Vector3(0.5f, 1.5f, 1.5f);
    [Tooltip("Multiplica el peso del ataque que acaba de hacer (para no repetir tanto).")]
    [SerializeField, Range(0f, 1f)] float repeatPenalty = 0.35f;

    [Header("Golpe frontal")]
    [Tooltip("Golpes seguidos (al azar entre los dos).")]
    [SerializeField] Vector2Int meleeHits = new Vector2Int(1, 3);
    [Tooltip("Carga del primer golpe y de los siguientes del combo (mas corta, para sorprender).")]
    [SerializeField] float meleeCharge = 0.7f;
    [SerializeField] float meleeComboCharge = 0.4f;
    [Tooltip("Largo y ancho del rectangulo del golpe.")]
    [SerializeField] float meleeLength = 3f;
    [SerializeField] float meleeWidth = 1.6f;
    [Tooltip("Desde donde empieza el rectangulo (distancia desde el centro del jefe).")]
    [SerializeField] float meleeStartOffset = 0.3f;
    [SerializeField] float meleeStrikeTime = 0.16f;
    [Tooltip("Velocidad del empujon hacia adelante al golpear.")]
    [SerializeField] float meleeLunge = 5f;
    [SerializeField] float meleeBetween = 0.25f;
    [SerializeField, Range(0f, 1f)] float meleeShake = 0.4f;

    [Header("Proyectiles en 360 grados")]
    [SerializeField] EnemyProjectile projectilePrefab;
    [Tooltip("Opcional: de donde salen (vacio = el centro del jefe).")]
    [SerializeField] Transform muzzle;
    [Tooltip("Cuantas lineas alrededor (siempre a la misma distancia entre ellas).")]
    [SerializeField, Range(3, 24)] int rays = 8;
    [Tooltip("Rafagas seguidas (al azar entre los dos). Cada una gira las lineas.")]
    [SerializeField] Vector2Int volleys = new Vector2Int(1, 3);
    [SerializeField] float rangedCharge = 0.9f;
    [SerializeField] float rangedBetween = 0.45f;
    [Tooltip("Largo y ancho de cada rectangulo de aviso.")]
    [SerializeField] float rangedTelegraphLength = 9f;
    [SerializeField] float rangedTelegraphWidth = 0.6f;
    [SerializeField] float projectileSpeed = 8f;
    [Tooltip("Marcado: la primera rafaga siempre tiene una linea apuntando al jugador.")]
    [SerializeField] bool firstVolleyAimsAtPlayer = true;

    [Header("Invocacion")]
    [Tooltip("Prefabs de enemigos que puede invocar (Muerte, Arlequin...).")]
    [SerializeField] Enemy[] summonPool;
    [SerializeField] Vector2Int summonCount = new Vector2Int(2, 3);
    [Tooltip("Maximo de enemigos invocados vivos a la vez.")]
    [SerializeField] int maxMinions = 4;
    [Tooltip("Segundos con los brazos arriba.")]
    [SerializeField] float summonCast = 0.8f;
    [Tooltip("Segundos que se ve el circulo antes de que aparezca cada enemigo.")]
    [SerializeField] float summonTelegraph = 0.6f;
    [Tooltip("A que distancia del jefe aparecen (minimo, maximo).")]
    [SerializeField] Vector2 summonRadius = new Vector2(2.5f, 5f);
    [SerializeField] float summonMinDistanceFromPlayer = 2.5f;
    [Tooltip("Segundos antes de poder volver a invocar.")]
    [SerializeField] float summonCooldown = 8f;
    [Tooltip("Opcional: sprite con los brazos arriba.")]
    [SerializeField] Sprite summonSprite;

    [Header("Saco")]
    [SerializeField] BossSack sack;
    [Tooltip("Opcional: de donde sale el saco (vacio = un poco encima del jefe).")]
    [SerializeField] Transform sackHand;
    [Tooltip("Radio del circulo: si el jugador esta dentro cuando cae el saco, queda atrapado.")]
    [SerializeField] float sackRadius = 1.6f;
    [SerializeField] float sackWindup = 0.9f;
    [SerializeField] float sackFlightTime = 0.55f;
    [SerializeField] float sackReturnTime = 0.5f;
    [SerializeField, Range(0f, 1f)] float sackLandShake = 0.45f;
    [Tooltip("Si el saco falla: segundos que el jefe queda 'vendido' (aturdido y sin atacar).")]
    [SerializeField] float stunTime = 2.5f;
    [Tooltip("Dano extra mientras esta aturdido (1,5 = 50% mas).")]
    [SerializeField] float vulnerableDamageMultiplier = 1.5f;
    [Tooltip("Opcional: sprite al lanzar el saco y sprite aturdido.")]
    [SerializeField] Sprite throwSprite;
    [SerializeField] Sprite stunnedSprite;

    [Header("Entrada (cinematica al entrar a la sala)")]
    [Tooltip("Marcado: al entrar, la camara enfoca al jefe con barras negras, ruge y empieza la pelea.")]
    [SerializeField] bool playIntro = true;
    [SerializeField, Range(0.3f, 1f)] float introZoom = 0.6f;
    [Tooltip("Segundos (reales) desde que empieza el enfoque hasta que ruge.")]
    [SerializeField] float introRoarDelay = 0.6f;
    [Tooltip("Segundos (reales) que se queda mirando al jefe despues de rugir.")]
    [SerializeField] float introHold = 1.1f;
    [SerializeField, Range(0f, 1f)] float introRoarShake = 0.6f;
    [Tooltip("Segundos (reales) que tarda la barra de vida en aparecer con fade in, DESPUES de quitarse la cinematica. La pelea empieza al terminar.")]
    [SerializeField] float barFadeIn = 0.6f;

    [Header("Derrota (cinematica hasta la explosion)")]
    [SerializeField, Range(0.3f, 1f)] float defeatZoom = 0.55f;
    [SerializeField, Range(0f, 1f)] float defeatHitShake = 0.7f;
    [SerializeField] float defeatHitStop = 0.15f;
    [Tooltip("Camara lenta al principio de la muerte.")]
    [SerializeField, Range(0.05f, 1f)] float defeatSlowScale = 0.35f;
    [Tooltip("Segundos (reales) de camara lenta.")]
    [SerializeField] float defeatSlowTime = 1f;
    [Tooltip("Segundos (reales) temblando, estirandose y parpadeando cada vez mas fuerte antes de explotar.")]
    [SerializeField] float buildUpTime = 2.4f;
    [Tooltip("Temblor maximo al final (unidades), giro maximo (grados) y squash & stretch maximo.")]
    [SerializeField] float maxShakeOffset = 0.12f;
    [SerializeField] float maxShakeRotation = 9f;
    [SerializeField] float maxPulseStretch = 0.28f;
    [Tooltip("Parpadeos blancos por segundo al empezar y justo antes de explotar.")]
    [SerializeField] Vector2 blinkSpeed = new Vector2(3f, 20f);
    [SerializeField] float explosionScale = 2.6f;
    [Tooltip("Tamano de la nube grande de la explosion final (0 = sin nube).")]
    [SerializeField] float bossPoofScale = 3.5f;
    [SerializeField, Range(0f, 1f)] float explosionShake = 1f;
    [Tooltip("Segundos (reales) despues de la explosion antes del fundido a negro.")]
    [SerializeField] float afterExplosion = 0.9f;

    Mode mode = Mode.Idle;
    Attack lastAttack = Attack.None;
    bool started;
    bool introDone;
    bool vulnerable;
    Room room;
    Sprite baseSprite;
    Vector2 lungeDir;
    float nextSummonTime;
    readonly List<Enemy> minions = new List<Enemy>();
    readonly List<TelegraphHandle> telegraphs = new List<TelegraphHandle>();

    public bool IsDone => defeated;
    protected override bool ContactKills => contactKills;
    bool Enraged => health <= maxHealth * enrageAt;
    float Pace => Enraged ? enragedPace : 1f;

    // ---------- Ciclo de vida ----------
    protected override void Awake()
    {
        base.Awake();
        room = GetComponentInParent<Room>();
        if (sr != null) baseSprite = sr.sprite;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Active = this;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (Active == this) Active = null;
    }

    /// La llama Room cuando el jugador entra (o reaparece): empieza la pelea si no habia empezado.
    public void Begin()
    {
        if (started || defeated) return;
        started = true;

        // Entrada: enfoque al jefe, rugido y despues la pelea (el cerebro espera mientras dura la cinematica)
        var cinematic = RoomCompleteCinematic.Instance;
        introDone = false;
        if (playIntro && cinematic != null && cinematic.BeginFocus(FocusPoint, introZoom, 0f, 0f))
            StartCoroutine(IntroRoutine(cinematic));
        else
            StartCoroutine(ShowBarThenFight());

        StartCoroutine(Brain());
    }

    Vector3 FocusPoint => body != null ? body.bounds.center : transform.position;

    void ShowBar()
    {
        var bar = BossHealthBar.Get();
        bar.Show(bossName, barFadeIn);
        bar.SetValue((float)health / maxHealth);
    }

    // La barra aparece con fade in y, cuando termina, empieza la pelea
    IEnumerator ShowBarThenFight()
    {
        ShowBar();
        yield return WaitReal(barFadeIn);
        introDone = true;
    }

    IEnumerator IntroRoutine(RoomCompleteCinematic cinematic)
    {
        yield return WaitReal(introRoarDelay);

        // Rugido: levanta los brazos, polvo, shake y aparece la barra de vida
        if (juice != null) juice.Play("ArmsUp", Vector2.zero, 0.9f, 1.3f);
        if (GameFeel.Instance != null) GameFeel.Instance.AddShake(introRoarShake);
        Vfx.Play(VfxKind.Dust, rb.position, Vector2.up, 2f);

        yield return WaitReal(introHold);
        yield return cinematic.EndFocus(true);

        // Ya sin barras negras: aparece la barra de vida y despues empieza la pelea
        yield return ShowBarThenFight();
    }

    /// Reinicio de la sala (entrar de nuevo desde la sala segura): todo desde cero.
    protected override void OnReset()
    {
        mode = Mode.Idle;
        started = false;
        introDone = false;
        vulnerable = false;
        lastAttack = Attack.None;
        lungeDir = Vector2.zero;
        nextSummonTime = 0f;
        HideTelegraphs();
        KillMinions(false);
        if (sack != null) sack.HideNow();
        RestoreSprite();
        if (BossHealthBar.Instance != null) BossHealthBar.Instance.Hide();
    }

    // ---------- Movimiento ----------
    protected override Vector2 ComputeDesiredVelocity()
    {
        Vector2 toPlayer = PlayerPosition - rb.position;
        if (mode != Mode.Stunned) FaceDirection(toPlayer);

        switch (mode)
        {
            case Mode.Walk:
                return toPlayer.magnitude > stopDistance ? Navigate(toPlayer) * moveSpeed : Vector2.zero;
            case Mode.Lunge:
                return lungeDir * meleeLunge;
            default:
                return Vector2.zero;
        }
    }

    // ---------- Cerebro ----------
    IEnumerator Brain()
    {
        while (!introDone) yield return null;               // espera cinematica + fade in de la barra
        yield return WaitActive(introDelay);
        while (!defeated)
        {
            mode = Mode.Walk;
            yield return WaitActive(Random.Range(walkTime.x, walkTime.y) * Pace);

            mode = Mode.Busy;
            Attack attack = Choose();
            switch (attack)
            {
                case Attack.Melee: yield return MeleeAttack(); break;
                case Attack.Ranged: yield return RangedAttack(); break;
                case Attack.Summon: yield return SummonAttack(); break;
                case Attack.Sack: yield return SackAttack(); break;
            }
            lastAttack = attack;

            mode = Mode.Idle;
            yield return WaitActive(recoverTime * Pace);
        }
    }

    Attack Choose()
    {
        float d = Vector2.Distance(PlayerPosition, rb.position);
        int band = d < nearDistance ? 0 : (d < farDistance ? 1 : 2);

        float melee = meleeWeights[band];
        float ranged = projectilePrefab != null ? rangedWeights[band] : 0f;
        float sackW = sack != null ? sackWeights[band] : 0f;
        bool canSummon = summonPool != null && summonPool.Length > 0 && AliveMinions() < maxMinions && Time.time >= nextSummonTime;
        float summon = canSummon ? summonWeights[band] : 0f;

        if (lastAttack == Attack.Melee) melee *= repeatPenalty;
        if (lastAttack == Attack.Ranged) ranged *= repeatPenalty;
        if (lastAttack == Attack.Sack) sackW *= repeatPenalty;
        if (lastAttack == Attack.Summon) summon *= repeatPenalty;

        float total = melee + ranged + sackW + summon;
        if (total <= 0f) return Attack.Melee;

        float r = Random.value * total;
        if ((r -= melee) < 0f) return Attack.Melee;
        if ((r -= ranged) < 0f) return Attack.Ranged;
        if ((r -= sackW) < 0f) return Attack.Sack;
        return Attack.Summon;
    }

    int Repeats(Vector2Int range) => Random.Range(range.x, Mathf.Max(range.x, range.y) + 1) + (Enraged ? enragedExtraRepeats : 0);

    // ---------- Golpe frontal ----------
    IEnumerator MeleeAttack()
    {
        int hits = Repeats(meleeHits);
        for (int i = 0; i < hits && !defeated; i++)
        {
            Vector2 dir = AimDir();
            FaceDirection(dir);
            float charge = (i == 0 ? meleeCharge : meleeComboCharge) * Pace;
            Vector2 origin = rb.position + dir * meleeStartOffset;

            Track(Telegraph.Rect(origin, dir, meleeLength, meleeWidth, charge + meleeStrikeTime));
            if (juice != null) juice.Play("LeanBack", dir, charge, 1.2f);
            yield return WaitActive(charge);

            // Golpe: empujon hacia adelante y dano en el rectangulo que se aviso
            if (juice != null) juice.Play("LeanForward", dir, meleeStrikeTime + 0.1f, 1.3f);
            lungeDir = dir;
            mode = Mode.Lunge;
            yield return WaitActive(meleeStrikeTime * 0.5f);

            Vector2 center = origin + dir * (meleeLength * 0.5f);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            foreach (var col in Physics2D.OverlapBoxAll(center, new Vector2(meleeLength, meleeWidth), angle))
                HazardUtil.TryKillPlayer(col, rb.position, false);
            Vfx.Play(VfxKind.Dust, center, dir, 1.4f);
            if (GameFeel.Instance != null) GameFeel.Instance.AddShake(meleeShake, dir);

            yield return WaitActive(meleeStrikeTime * 0.5f);
            mode = Mode.Busy;
            HideTelegraphs();
            yield return WaitActive(meleeBetween * Pace);
        }
    }

    // ---------- Proyectiles en 360 grados ----------
    IEnumerator RangedAttack()
    {
        int count = Repeats(volleys);
        float step = 360f / rays;
        Vector2 toPlayer = AimDir();
        float offset = firstVolleyAimsAtPlayer ? Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg : Random.Range(0f, step);

        for (int v = 0; v < count && !defeated; v++)
        {
            // Cada rafaga gira las lineas: los huecos seguros de la anterior ya no lo son
            if (v > 0) offset += step * Random.Range(0.35f, 0.65f);

            Vector2 origin = muzzle != null ? (Vector2)muzzle.position : rb.position;
            var dirs = new Vector2[rays];
            float charge = rangedCharge * Pace;
            for (int i = 0; i < rays; i++)
            {
                float a = (offset + step * i) * Mathf.Deg2Rad;
                dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Track(Telegraph.Rect(origin, dirs[i], rangedTelegraphLength, rangedTelegraphWidth, charge));
            }

            if (juice != null) juice.Play("Charge", Vector2.zero, charge, 1.2f);
            yield return WaitActive(charge);

            if (juice != null) juice.Play("Release");
            foreach (var dir in dirs)
            {
                EnemyProjectile p = Instantiate(projectilePrefab, origin + dir * 0.6f, Quaternion.identity,
                                                room != null ? room.transform : null);
                p.Launch(dir, projectileSpeed);
                Track(p.gameObject);
            }
            Vfx.Play(VfxKind.Dust, origin, Vector2.up, 1.2f);
            if (GameFeel.Instance != null) GameFeel.Instance.AddShake(0.3f);

            yield return WaitActive(rangedBetween * Pace);
        }
    }

    // ---------- Invocacion ----------
    IEnumerator SummonAttack()
    {
        SwapSprite(summonSprite);
        if (juice != null) juice.Play("ArmsUp", Vector2.zero, summonCast + summonTelegraph);
        yield return WaitActive(summonCast * 0.5f);

        int count = Mathf.Min(Random.Range(summonCount.x, Mathf.Max(summonCount.x, summonCount.y) + 1), maxMinions - AliveMinions());
        var spots = new List<Vector2>();
        for (int i = 0; i < count; i++)
        {
            Vector2 p = FindSummonSpot();
            spots.Add(p);
            Track(Telegraph.Circle(p, 0.7f, summonTelegraph));
        }
        yield return WaitActive(summonTelegraph);

        foreach (var p in spots)
        {
            Enemy prefab = summonPool[Random.Range(0, summonPool.Length)];
            if (prefab == null) continue;
            Enemy e = Instantiate(prefab, p, Quaternion.identity, room != null ? room.transform : null);
            minions.Add(e);
            Vfx.Play(VfxKind.Dust, p, Vector2.up, 1.3f);
        }
        nextSummonTime = Time.time + summonCooldown;

        yield return WaitActive(summonCast * 0.5f);
        RestoreSprite();
    }

    Vector2 FindSummonSpot()
    {
        Vector2 center = room != null ? (Vector2)room.CameraPosition : rb.position;
        Vector2 half = room != null ? room.ViewSize * 0.5f - new Vector2(1.5f, 1.2f) : new Vector2(6f, 3f);
        Vector2 best = rb.position + Random.insideUnitCircle.normalized * summonRadius.x;

        for (int i = 0; i < 30; i++)
        {
            Vector2 p = rb.position + Random.insideUnitCircle.normalized * Random.Range(summonRadius.x, summonRadius.y);
            if (Mathf.Abs(p.x - center.x) > half.x || Mathf.Abs(p.y - center.y) > half.y) continue;
            if (Vector2.Distance(p, PlayerPosition) < summonMinDistanceFromPlayer) continue;
            if (obstacleMask.value != 0 && Physics2D.OverlapCircle(p, 0.5f, obstacleMask) != null) continue;
            return p;
        }
        return best;
    }

    // ---------- Saco ----------
    IEnumerator SackAttack()
    {
        if (sack == null) yield break;

        Vector2 target = ClampToRoom(PlayerPosition, 1.2f);
        float windup = sackWindup * Pace;
        SwapSprite(throwSprite);
        TelegraphHandle circle = Track(Telegraph.Circle(target, sackRadius, windup + sackFlightTime));
        if (juice != null) juice.Play("Charge", Vector2.zero, windup, 1.3f);
        yield return WaitActive(windup);

        if (juice != null) juice.Play("Release");
        Vector2 from = sackHand != null ? (Vector2)sackHand.position : rb.position + Vector2.up * 0.8f;
        yield return sack.Fly(from, target, sackFlightTime);
        if (circle != null) circle.Hide();
        sack.Land(sackLandShake);
        RestoreSprite();

        PlayerHealth ph = Player;
        bool inside = ph != null && !ph.IsDead && !ph.IsInvulnerable && GameIsPlaying
                      && Vector2.Distance(ph.transform.position, target) <= sackRadius;

        if (inside)
        {
            // Atrapado: el jugador mashea para salir. El jefe espera riendose.
            yield return sack.Capture(ph.Controller, rb.position, null);
            yield return WaitActive(0.3f);
            yield return sack.ReturnTo(transform, sackReturnTime);
        }
        else
        {
            // Fallo: queda "vendido" un momento y el jugador puede pegarle tranquilo
            mode = Mode.Stunned;
            vulnerable = true;
            SwapSprite(stunnedSprite);
            float left = stunTime;
            while (left > 0f && !defeated)
            {
                if (juice != null) juice.Play("Stunned");
                float chunk = Mathf.Min(1f, left);
                yield return WaitActive(chunk);
                left -= chunk;
            }
            vulnerable = false;
            RestoreSprite();
            mode = Mode.Busy;
            yield return sack.ReturnTo(transform, sackReturnTime);
        }
    }

    // ---------- Recibir golpes ----------
    // Empuje sutil, sin aturdir: puede seguir atacando aunque le peguen
    protected override void TakeHit(int damage, Vector2 direction, float knockbackMultiplier = 1f, float feelMultiplier = 1f)
    {
        if (defeated) return;
        int dmg = vulnerable ? Mathf.CeilToInt(damage * vulnerableDamageMultiplier) : damage;
        health -= dmg;
        knockVelocity = direction * hitKnockback * knockbackMultiplier;

        if (flasher != null) flasher.Flash(hitFlashTime);
        if (juice != null) juice.Play("Hit", Vector2.zero, 0f, 0.5f);
        if (GameFeel.Instance != null) GameFeel.Instance.EnemyHit(direction, feelMultiplier * hitFeelBoost);
        Vfx.Play(VfxKind.HitEnemy, body != null ? (Vector2)body.bounds.center : rb.position, direction,
                 hitVfxScale * (feelMultiplier > 1f ? 1.6f : 1.2f));
        if (BossHealthBar.Instance != null) BossHealthBar.Instance.SetValue((float)Mathf.Max(0, health) / maxHealth);

        if (health <= 0) Defeat(direction);
    }

    // ---------- Derrota ----------
    protected override void Defeat(Vector2 direction)
    {
        defeated = true;
        NotifyDefeated();
        StopAllCoroutines();
        mode = Mode.Idle;
        vulnerable = false;
        HideTelegraphs();
        ClearProjectiles();
        KillMinions(true);
        if (sack != null) sack.HideNow();
        if (body != null) body.enabled = false;
        StartCoroutine(DefeatRoutine());
    }

    // Enfoque -> camara lenta -> tiembla, se estira y parpadea cada vez mas fuerte -> explota -> fundido a negro -> victoria
    IEnumerator DefeatRoutine()
    {
        if (BossHealthBar.Instance != null) BossHealthBar.Instance.SetValue(0f);

        var cinematic = RoomCompleteCinematic.Instance;
        bool focused = cinematic != null && cinematic.BeginFocus(FocusPoint, defeatZoom, defeatHitShake, defeatHitStop);
        yield return WaitReal(defeatHitStop + 0.02f);

        float t = 0f;
        float nextPuff = 0.3f;
        while (t < buildUpTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / buildUpTime);

            // Camara lenta solo al principio
            Time.timeScale = t < defeatSlowTime ? defeatSlowScale : 1f;

            // Temblor + squash & stretch que crecen
            if (juice != null)
            {
                float n = Time.unscaledTime * 35f;
                Vector2 jitter = new Vector2(Mathf.PerlinNoise(n, 3.1f) - 0.5f, Mathf.PerlinNoise(7.7f, n) - 0.5f) * 2f * maxShakeOffset * k;
                float rot = (Mathf.PerlinNoise(n, 11.3f) - 0.5f) * 2f * maxShakeRotation * k;
                float pulse = Mathf.Sin(t * Mathf.Lerp(6f, 22f, k)) * maxPulseStretch * k;
                juice.SetOverlay(rot, pulse, jitter);
            }

            // Parpadeo blanco cada vez mas rapido y mas intenso
            if (flasher != null)
            {
                float freq = Mathf.Lerp(blinkSpeed.x, blinkSpeed.y, k * k);
                float wave = 0.5f + 0.5f * Mathf.Sin(t * freq * Mathf.PI * 2f);
                flasher.SetFlashAmount(wave * Mathf.Lerp(0.25f, 1f, k));
            }

            // Bocanadas de polvo cada vez mas seguidas
            if (t >= nextPuff)
            {
                Vfx.Play(VfxKind.Dust, rb.position + Random.insideUnitCircle * 0.6f, Vector2.up, Mathf.Lerp(0.6f, 1.2f, k));
                nextPuff = t + Mathf.Lerp(0.35f, 0.1f, k);
            }
            yield return null;
        }
        Time.timeScale = 1f;

        // Explosion
        if (juice != null) juice.ClearOverlay();
        if (flasher != null) flasher.SetFlashAmount(0f);
        Vfx.Play(VfxKind.Explosion, FocusPoint, Vector2.zero, explosionScale);
        if (bossPoofScale > 0f) Vfx.Play(VfxKind.Poof, FocusPoint, Vector2.up, bossPoofScale);
        if (GameFeel.Instance != null) GameFeel.Instance.AddShake(explosionShake);
        if (sr != null) sr.enabled = false;               // el jefe desaparece en la explosion
        if (BossHealthBar.Instance != null) BossHealthBar.Instance.Hide();

        yield return WaitReal(afterExplosion);

        // Fundido a negro -> pantalla de victoria -> se aclara
        var gm = GameManager.Instance;
        System.Action onBlack = () =>
        {
            if (focused && cinematic != null) cinematic.EndFocusImmediate();
            if (gm != null) gm.OnBossDefeated();
            if (sr != null) sr.enabled = true;            // para el proximo reinicio
            gameObject.SetActive(false);
        };

        ScreenFader fader = ScreenFader.Instance;
        if (fader != null && !fader.IsBusy) fader.FadeOutThen(onBlack, true);
        else onBlack();
    }

    static IEnumerator WaitReal(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // ---------- Ayudantes ----------
    // Espera que solo avanza mientras se juega y el jugador esta vivo (se pausa en transiciones y al morir)
    IEnumerator WaitActive(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (GameIsPlaying && PlayerAlive) t += Time.deltaTime;
            yield return null;
        }
    }

    Vector2 AimDir()
    {
        Vector2 d = PlayerCenter - rb.position;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right;
    }

    Vector2 ClampToRoom(Vector2 p, float margin)
    {
        if (room == null) return p;
        Vector2 c = room.CameraPosition;
        Vector2 half = room.ViewSize * 0.5f - Vector2.one * margin;
        return new Vector2(Mathf.Clamp(p.x, c.x - half.x, c.x + half.x), Mathf.Clamp(p.y, c.y - half.y, c.y + half.y));
    }

    TelegraphHandle Track(TelegraphHandle h)
    {
        if (h != null) telegraphs.Add(h);
        return h;
    }

    void HideTelegraphs()
    {
        foreach (var h in telegraphs) if (h != null) h.Hide();
        telegraphs.Clear();
    }

    int AliveMinions()
    {
        minions.RemoveAll(m => m == null || !m.gameObject.activeSelf);
        return minions.Count;
    }

    void KillMinions(bool puff)
    {
        foreach (var m in minions)
        {
            if (m == null) continue;
            if (puff && m.gameObject.activeSelf) Vfx.Play(VfxKind.Dust, m.transform.position, Vector2.up, 1.2f);
            Destroy(m.gameObject);
        }
        minions.Clear();
    }

    void SwapSprite(Sprite s)
    {
        if (s != null && sr != null) sr.sprite = s;
    }

    void RestoreSprite()
    {
        if (sr != null && baseSprite != null) sr.sprite = baseSprite;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, nearDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, farDistance);
    }
}
