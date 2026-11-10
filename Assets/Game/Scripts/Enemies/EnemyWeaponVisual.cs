using System.Collections;
using UnityEngine;

public class EnemyWeaponVisual : MonoBehaviour
{
    [Tooltip("El SpriteRenderer del arma (hijo de este objeto).")]
    [SerializeField] SpriteRenderer weaponRenderer;
    [Tooltip("El sprite del enemigo, para ordenar el arma delante o detras de el.")]
    [SerializeField] SpriteRenderer ownerRenderer;
    [Tooltip("JuiceAnimator del sprite del arma (solo el arco lo necesita).")]
    [SerializeField] JuiceAnimator weaponJuice;
    [Tooltip("Altura de las manos respecto al pivote del enemigo.")]
    [SerializeField] Vector2 originOffset = new Vector2(0f, 0.4f);
    [Tooltip("Cuanto se aleja el arma del centro de las manos.")]
    [SerializeField] float distance = 0.5f;
    [Tooltip("Grados por segundo con los que gira para seguir al jugador (0 = instantaneo).")]
    [SerializeField] float turnSpeed = 540f;
    [SerializeField] bool flipWhenAimingLeft = true;

    [Header("Espada: carga y golpe")]
    [Tooltip("Cuanto echa la espada hacia atras al cargar (grados).")]
    [SerializeField] float chargeBackAngle = 55f;
    [Tooltip("Vibracion de la espada mientras carga (grados). Menor que en otros enemigos.")]
    [SerializeField] float chargeShake = 2.5f;
    [Tooltip("Hasta donde llega el golpe, al otro lado del jugador (grados).")]
    [SerializeField] float strikeAngle = 70f;
    [Tooltip("Progreso del golpe: tiempo (0 a 1) en X, avance de la carga al final del golpe (0 a 1) en Y. Puede pasarse de 1.")]
    [SerializeField] AnimationCurve strikeCurve = DefaultStrikeCurve();
    [Tooltip("Lo que tarda la espada en volver a su sitio despues del golpe.")]
    [SerializeField] float returnTime = 0.25f;

    Transform owner;
    PlayerHealth player;
    Collider2D playerCollider;
    float angle;
    float offsetAngle;
    bool initialized;
    Coroutine anim;

    static AnimationCurve DefaultStrikeCurve()
    {
        var curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.55f, 1.08f),
            new Keyframe(1f, 1f));
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
        return curve;
    }

    void Awake()
    {
        owner = transform.parent != null ? transform.parent : transform;
        if (weaponJuice == null && weaponRenderer != null) weaponJuice = weaponRenderer.GetComponent<JuiceAnimator>();
    }

    void Start()
    {
        if (weaponRenderer != null) weaponRenderer.transform.localPosition = new Vector3(distance, 0f, 0f);
    }

    void OnEnable()
    {
        initialized = false;
        offsetAngle = 0f;
    }

    void LateUpdate()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerHealth>();
            if (player != null) playerCollider = player.GetComponent<Collider2D>();
        }

        Vector2 origin = (Vector2)owner.position + originOffset;
        transform.position = origin;

        if (player != null)
        {
            Vector2 target = playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)player.transform.position;
            Vector2 d = target - origin;
            if (d.sqrMagnitude > 0.0001f)
            {
                float desired = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                angle = (!initialized || turnSpeed <= 0f)
                    ? desired
                    : Mathf.MoveTowardsAngle(angle, desired, turnSpeed * Time.deltaTime);
                initialized = true;
            }
        }
        transform.rotation = Quaternion.Euler(0f, 0f, angle + offsetAngle);

        if (weaponRenderer == null) return;
        if (flipWhenAimingLeft) weaponRenderer.flipY = Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) > 90f;

        if (ownerRenderer != null)
        {
            weaponRenderer.sortingLayerID = ownerRenderer.sortingLayerID;
            bool aimingUp = Mathf.Sin(angle * Mathf.Deg2Rad) > 0.35f;
            weaponRenderer.sortingOrder = ownerRenderer.sortingOrder + (aimingUp ? -1 : 1);
        }
    }

    // ---------- Arco ----------
    /// El arquero empieza a apuntar: el arco se tensa durante 'duration' segundos.
    public void Draw(float duration)
    {
        if (weaponJuice != null) weaponJuice.Play("BowDraw", Vector2.zero, duration);
    }

    /// Dispara: el arco se aplasta y rebota.
    public void Release()
    {
        if (weaponJuice != null) weaponJuice.Play("BowRelease");
    }

    // ---------- Espada ----------
    /// Carga: la espada se echa hacia atras con una vibracion corta, y se queda atras hasta el golpe.
    public void Charge(float duration)
    {
        StartAnim(ChargeRoutine(Mathf.Max(0.02f, duration)));
    }

    /// Golpe: la espada pasa de atras a adelante siguiendo la curva y despues vuelve a su sitio.
    public void Strike(float duration)
    {
        StartAnim(StrikeRoutine(Mathf.Max(0.02f, duration)));
    }

    /// Corta cualquier movimiento de la espada (por ejemplo, si el enemigo recibe un golpe).
    public void CancelAttack()
    {
        StopAnim();
        offsetAngle = 0f;
    }

    /// Corta todo: arco y espada.
    public void Cancel()
    {
        if (weaponJuice != null) weaponJuice.StopAll();
        CancelAttack();
    }

    void StartAnim(IEnumerator routine)
    {
        StopAnim();
        anim = StartCoroutine(routine);
    }

    void StopAnim()
    {
        if (anim != null) StopCoroutine(anim);
        anim = null;
    }

    IEnumerator ChargeRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            offsetAngle = chargeBackAngle * Mathf.SmoothStep(0f, 1f, k) + Mathf.Sin(t * 70f) * chargeShake * k;
            yield return null;
        }
        offsetAngle = chargeBackAngle;     // se queda atras hasta que llegue el golpe
        anim = null;
    }

    IEnumerator StrikeRoutine(float duration)
    {
        float start = offsetAngle;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            offsetAngle = Mathf.LerpUnclamped(start, -strikeAngle, strikeCurve.Evaluate(k));
            yield return null;
        }

        float from = offsetAngle;
        t = 0f;
        while (t < returnTime)
        {
            t += Time.deltaTime;
            offsetAngle = Mathf.Lerp(from, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.02f, returnTime))));
            yield return null;
        }
        offsetAngle = 0f;
        anim = null;
    }
}
