using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public class PlayerAttack : MonoBehaviour
{
    enum Mode { Idle, Normal, Charging, ChargedSwing }

    [Header("Golpe normal")]
    [SerializeField] AttackHitbox hitbox;
    [Tooltip("Que tan lejos de la mano aparece el hitbox, en la direccion del mouse.")]
    [SerializeField] float hitboxDistance = 0.9f;
    [Tooltip("Solo si no hay PlayerAim: altura de la mano.")]
    [SerializeField] Vector2 fallbackCenterOffset = new Vector2(0f, 0.4f);
    [SerializeField] float windUp = 0.05f;
    [SerializeField] float activeTime = 0.15f;
    [SerializeField] float cooldown = 0.25f;
    [SerializeField] float moveSpeedWhileAttacking = 0.4f;
    [SerializeField] SlashEffect slashEffect;      // opcional
    [SerializeField] WeaponVisual weapon;          // opcional: el sprite del arma

    [Header("Ataque cargado: carga")]
    [Tooltip("Barra de bloques. Si esta vacio, el ataque cargado siempre esta disponible.")]
    [SerializeField] ChargeAttackMeter meter;
    [Tooltip("Segundos manteniendo el click (despues del golpe normal) para empezar a cargar.")]
    [SerializeField] float chargeStartDelay = 0.3f;
    [Tooltip("Segundos para llegar a la carga maxima.")]
    [SerializeField] float maxChargeTime = 1.2f;
    [Tooltip("Carga minima (0 a 1) para que soltar el click haga el ataque. Menos que esto se cancela sin gastar la barra.")]
    [SerializeField, Range(0f, 1f)] float minChargeToRelease = 0.2f;
    [Tooltip("Si mantienes el click este tiempo y la barra no esta llena, se muestra el aviso rojo.")]
    [SerializeField] float deniedHoldTime = 1f;

    [Header("Ataque cargado: swing")]
    [SerializeField] float chargedWindUp = 0.06f;
    [SerializeField] float chargedActiveTime = 0.22f;
    [Tooltip("Tiempo de recuperacion tras el swing cargado (el movimiento va lento).")]
    [SerializeField] float chargedRecovery = 0.35f;
    [SerializeField, Range(0f, 1f)] float chargedRecoverySpeed = 0.3f;
    [Tooltip("Tamano de la hitbox con la carga minima y con la maxima (multiplica su tamano normal).")]
    [SerializeField] Vector2 chargedHitboxScale = new Vector2(1.4f, 2.6f);
    [Tooltip("Cuanto se aleja la hitbox con la carga maxima (1 = igual que el golpe normal).")]
    [SerializeField] float chargedDistanceMultiplier = 1.5f;
    [Tooltip("Dano con la carga minima y con la maxima (el golpe normal hace 1).")]
    [SerializeField] Vector2Int chargedDamage = new Vector2Int(2, 4);
    [Tooltip("Cuanto crece la espada con la carga maxima.")]
    [SerializeField] float chargedWeaponScale = 2.2f;
    [SerializeField] float chargedKnockbackMultiplier = 1.8f;
    [Tooltip("Sensacion de golpe (shake y hitstop) respecto al golpe normal.")]
    [SerializeField] float chargedFeelMultiplier = 2.2f;

    [Header("Ataque cargado: aspecto del personaje")]
    [Tooltip("Inclinacion hacia atras del cuerpo con la carga maxima (grados).")]
    [SerializeField] float leanBackAngle = 16f;
    [Tooltip("Vibracion del cuerpo con la carga (unidades) que crece con la carga.")]
    [SerializeField] float shakeAmplitude = 0.05f;
    [SerializeField] float shakeRotation = 3f;
    [SerializeField] float shakeFrequency = 40f;
    [Tooltip("Parpadeo en blanco al llegar al maximo: tiempo (0 a 1) en X, cuanto blanco en Y (un ciclo).")]
    [SerializeField] AnimationCurve maxBlinkCurve = DefaultBlinkCurve();
    [SerializeField] float maxBlinkPeriod = 0.3f;

    PlayerController controller;
    PlayerAnimator playerAnimator;
    PlayerAim aim;
    JuiceAnimator bodyJuice;
    SpriteFlasher bodyFlasher;
    Mode mode = Mode.Idle;
    Coroutine routine;
    float nextAttackTime;
    float holdTimer;
    bool holding;
    bool deniedShown;
    float charge;
    Vector3 baseHitboxScale = Vector3.one;
    Vector3 baseSlashScale = Vector3.one;

    // Lo leen los enemigos (y otros IHittable) para saber como pega este golpe
    public int CurrentDamage { get; private set; } = 1;
    public float KnockbackMultiplier { get; private set; } = 1f;
    public float HitFeelMultiplier { get; private set; } = 1f;

    static AnimationCurve DefaultBlinkCurve()
    {
        var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
        return curve;
    }

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        playerAnimator = GetComponent<PlayerAnimator>();
        aim = GetComponent<PlayerAim>();
        if (meter == null) meter = GetComponent<ChargeAttackMeter>();

        if (hitbox != null)
        {
            baseHitboxScale = hitbox.transform.localScale;
            hitbox.gameObject.SetActive(false);
        }
        if (slashEffect != null) baseSlashScale = slashEffect.transform.localScale;
    }

    void Start()
    {
        // Squash & stretch y blanco del cuerpo del jugador (los mismos del JuiceAnimator del Visual)
        bodyJuice = controller.Juice;
        if (bodyJuice != null)
        {
            bodyFlasher = bodyJuice.GetComponent<SpriteFlasher>();
            if (bodyFlasher == null && bodyJuice.GetComponent<SpriteRenderer>() != null)
                bodyFlasher = bodyJuice.gameObject.AddComponent<SpriteFlasher>();
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        bool pressedThisFrame = (kb != null && kb.jKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        bool held = (kb != null && kb.jKey.isPressed) || (mouse != null && mouse.leftButton.isPressed);

        // Sin control (muerte, transiciones) o en dash: se cancela la carga
        if (!controller.InputEnabled || controller.IsDashing)
        {
            if (mode == Mode.Charging) CancelCharge();
            holding = false;
            holdTimer = 0f;
            return;
        }

        if (pressedThisFrame)
        {
            holding = true;
            holdTimer = 0f;
            deniedShown = false;
        }
        if (!held)
        {
            holding = false;
            holdTimer = 0f;
        }
        else if (holding)
        {
            holdTimer += Time.deltaTime;
        }

        switch (mode)
        {
            case Mode.Idle:
                bool canAct = !controller.IsAirborne && Time.time >= nextAttackTime;
                if (pressedThisFrame && canAct)
                    routine = StartCoroutine(NormalAttackRoutine());
                else if (holding && canAct && holdTimer >= chargeStartDelay)
                    TryBeginCharge();
                break;

            case Mode.Charging:
                UpdateCharging(held);
                break;
        }
    }

    // ---------- Golpe normal ----------
    IEnumerator NormalAttackRoutine()
    {
        mode = Mode.Normal;
        controller.SpeedMultiplier = moveSpeedWhileAttacking;
        CurrentDamage = 1;
        KnockbackMultiplier = 1f;
        HitFeelMultiplier = 1f;

        // La direccion se fija al empezar el golpe.
        Vector2 dir = aim != null ? aim.Direction : controller.FacingVector;
        Vector2 origin = aim != null ? aim.Origin : (Vector2)transform.position + fallbackCenterOffset;
        PlaceHitbox(origin, dir, hitboxDistance, 1f);

        if (playerAnimator != null) playerAnimator.PlayAttack();
        if (weapon != null) weapon.Swing(windUp + activeTime);

        yield return new WaitForSeconds(windUp);
        hitbox.gameObject.SetActive(true);
        if (slashEffect != null) slashEffect.Play(hitbox.transform.position, dir);
        yield return new WaitForSeconds(activeTime);
        hitbox.gameObject.SetActive(false);

        controller.SpeedMultiplier = 1f;
        nextAttackTime = Time.time + cooldown;
        mode = Mode.Idle;
        routine = null;
    }

    void PlaceHitbox(Vector2 origin, Vector2 dir, float dist, float scale)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        hitbox.transform.position = origin + dir * dist;
        hitbox.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        hitbox.transform.localScale = baseHitboxScale * scale;
    }

    // ---------- Carga ----------
    void TryBeginCharge()
    {
        if (meter != null && !meter.IsFull)
        {
            // Sin la barra llena: no se carga. Tras mantener el click un rato se avisa en rojo.
            if (holdTimer >= deniedHoldTime && !deniedShown)
            {
                deniedShown = true;
                meter.ShowDenied();
            }
            return;
        }

        mode = Mode.Charging;
        charge = 0f;
        controller.SpeedMultiplier = 0f;      // no se puede mover mientras carga (si apuntar con el mouse)
    }

    void UpdateCharging(bool held)
    {
        charge = Mathf.Min(1f, charge + Time.deltaTime / Mathf.Max(0.05f, maxChargeTime));
        bool atMax = charge >= 1f;

        ApplyChargeVisuals(atMax);
        if (weapon != null) weapon.SetCharge(charge);

        if (!held)
        {
            if (charge >= minChargeToRelease) routine = StartCoroutine(ChargedSwingRoutine(charge));
            else CancelCharge();
        }
    }

    // Vibracion incremental con inclinacion hacia atras; al maximo: sin vibracion y parpadeo blanco por curva
    void ApplyChargeVisuals(bool atMax)
    {
        if (bodyJuice != null)
        {
            // Se inclina hacia atras: lejos de hacia donde apunta
            float sign = aim != null && aim.Direction.x < 0f ? -1f : 1f;
            float lean = leanBackAngle * charge * sign;

            Vector2 shake = Vector2.zero;
            float rotJitter = 0f;
            if (!atMax)
            {
                float t = Time.unscaledTime * shakeFrequency;
                shake = new Vector2(Mathf.PerlinNoise(11f, t) - 0.5f, Mathf.PerlinNoise(37f, t) - 0.5f) * 2f * shakeAmplitude * charge;
                rotJitter = (Mathf.PerlinNoise(73f, t) - 0.5f) * 2f * shakeRotation * charge;
            }
            bodyJuice.SetOverlay(lean + rotJitter, -0.06f * charge, shake);
        }

        if (bodyFlasher != null)
        {
            float phase = Mathf.Repeat(Time.unscaledTime / Mathf.Max(0.05f, maxBlinkPeriod), 1f);
            bodyFlasher.SetFlashAmount(atMax ? maxBlinkCurve.Evaluate(phase) : 0f);
        }
    }

    void ClearChargeVisuals()
    {
        if (bodyJuice != null) bodyJuice.ClearOverlay();
        if (bodyFlasher != null) bodyFlasher.SetFlashAmount(0f);
        if (weapon != null) weapon.SetCharge(0f);
    }

    void CancelCharge()
    {
        charge = 0f;
        ClearChargeVisuals();
        controller.SpeedMultiplier = 1f;
        mode = Mode.Idle;
    }

    // ---------- Swing cargado ----------
    IEnumerator ChargedSwingRoutine(float c)
    {
        mode = Mode.ChargedSwing;
        ClearChargeVisuals();
        if (meter != null) meter.ConsumeAll();      // se vacia por completo
        charge = 0f;

        // Fuerza segun cuanto se cargo
        CurrentDamage = Mathf.RoundToInt(Mathf.Lerp(chargedDamage.x, chargedDamage.y, c));
        KnockbackMultiplier = chargedKnockbackMultiplier;
        HitFeelMultiplier = chargedFeelMultiplier;
        float hitboxScale = Mathf.Lerp(chargedHitboxScale.x, chargedHitboxScale.y, c);
        float weaponScale = Mathf.Lerp(1f, chargedWeaponScale, c);
        float distance = hitboxDistance * Mathf.Lerp(1f, chargedDistanceMultiplier, c);

        Vector2 dir = aim != null ? aim.Direction : controller.FacingVector;
        Vector2 origin = aim != null ? aim.Origin : (Vector2)transform.position + fallbackCenterOffset;
        PlaceHitbox(origin, dir, distance, hitboxScale);

        if (weapon != null) weapon.SwingCharged(chargedWindUp + chargedActiveTime, weaponScale);

        yield return new WaitForSeconds(chargedWindUp);
        hitbox.gameObject.SetActive(true);
        if (slashEffect != null)
        {
            slashEffect.transform.localScale = baseSlashScale * Mathf.Max(1f, hitboxScale * 0.8f);
            slashEffect.Play(hitbox.transform.position, dir);
        }
        yield return new WaitForSeconds(chargedActiveTime);
        hitbox.gameObject.SetActive(false);
        RestoreAttackStats();

        // Recuperacion: se mueve despacio un momento
        controller.SpeedMultiplier = chargedRecoverySpeed;
        yield return new WaitForSeconds(chargedRecovery);
        controller.SpeedMultiplier = 1f;

        nextAttackTime = Time.time + cooldown;
        mode = Mode.Idle;
        routine = null;
    }

    void RestoreAttackStats()
    {
        CurrentDamage = 1;
        KnockbackMultiplier = 1f;
        HitFeelMultiplier = 1f;
        if (hitbox != null) hitbox.transform.localScale = baseHitboxScale;
        if (slashEffect != null) slashEffect.transform.localScale = baseSlashScale;
    }

    /// Corta cualquier ataque o carga (por ejemplo, al morir).
    public void CancelAttack()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        mode = Mode.Idle;
        charge = 0f;
        holding = false;
        holdTimer = 0f;

        if (hitbox != null) hitbox.gameObject.SetActive(false);
        RestoreAttackStats();
        ClearChargeVisuals();
        if (controller != null) controller.SpeedMultiplier = 1f;
        if (playerAnimator != null) playerAnimator.CancelAttack();
        if (weapon != null) weapon.CancelSwing();
        if (slashEffect != null) slashEffect.Stop();
    }
}
