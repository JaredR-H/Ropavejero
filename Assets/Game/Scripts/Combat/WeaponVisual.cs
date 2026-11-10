using System.Collections;
using UnityEngine;

public class WeaponVisual : MonoBehaviour
{
    [SerializeField] PlayerAim aim;
    [Tooltip("El SpriteRenderer del arma (hijo de este objeto).")]
    [SerializeField] SpriteRenderer weaponRenderer;
    [Tooltip("El SpriteRenderer del jugador, para ordenar el arma delante o detras de el.")]
    [SerializeField] SpriteRenderer playerRenderer;
    [Tooltip("Cuanto se aleja el arma de la mano.")]
    [SerializeField] float distance = 0.45f;
    [Tooltip("Angulo total del arco al golpear.")]
    [SerializeField] float swingArc = 110f;
    [Tooltip("Progreso del golpe: tiempo (0 a 1) en el eje X, avance del arco en el eje Y (0 = inicio, 1 = final). " +
             "Valores fuera de 0 a 1 dan anticipacion (menor que 0) o rebote (mayor que 1).")]
    [SerializeField] AnimationCurve swingCurve = DefaultSwingCurve();
    [Tooltip("Voltea el sprite en vertical al apuntar a la izquierda para que no quede de cabeza.")]
    [SerializeField] bool flipWhenAimingLeft = true;

    [Header("Ataque cargado")]
    [Tooltip("Cuanto se echa hacia atras la espada con la carga al maximo (grados).")]
    [SerializeField] float chargePullAngle = 75f;
    [Tooltip("Vibracion de la espada mientras se carga (grados).")]
    [SerializeField] float chargeShake = 3f;
    [Tooltip("El arco del swing cargado es este multiplo del arco de la carga hacia atras.")]
    [SerializeField] float chargedArcMultiplier = 1.5f;
    [Tooltip("Crecimiento de la espada durante el swing cargado: tiempo (0 a 1) en X, cuanto de lo que crece (0 a 1) en Y.")]
    [SerializeField] AnimationCurve chargedScaleCurve = DefaultScaleCurve();

    float swingOffset;
    float chargeAmount;
    float scaleFactor = 1f;
    int swingSign = 1;
    Coroutine swing;
    Vector3 baseWeaponScale = Vector3.one;

    static AnimationCurve DefaultSwingCurve()
    {
        var curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.18f, -0.12f),    // anticipacion: echa el arma hacia atras
            new Keyframe(0.5f, 1.08f),      // golpe rapido que se pasa un poco
            new Keyframe(0.78f, 0.98f),
            new Keyframe(1f, 1f));
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
        return curve;
    }

    static AnimationCurve DefaultScaleCurve()
    {
        var curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 1f),         // crece rapido al soltar
            new Keyframe(0.75f, 1f),
            new Keyframe(1f, 0.85f));
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
        return curve;
    }

    void Awake()
    {
        if (aim == null) aim = GetComponentInParent<PlayerAim>();
        if (weaponRenderer != null) baseWeaponScale = weaponRenderer.transform.localScale;
    }

    void LateUpdate()
    {
        if (aim == null) return;

        float angle = aim.Angle;
        transform.position = aim.Origin;

        // Carga: la espada se echa hacia atras (del lado desde el que empezara el siguiente swing) y vibra
        float chargeRot = 0f;
        if (chargeAmount > 0f)
        {
            chargeRot = chargePullAngle * swingSign * chargeAmount;
            if (chargeAmount < 1f)
                chargeRot += (Mathf.PerlinNoise(5f, Time.unscaledTime * 40f) - 0.5f) * 2f * chargeShake * chargeAmount;
        }
        transform.rotation = Quaternion.Euler(0f, 0f, angle + swingOffset + chargeRot);

        if (weaponRenderer == null) return;

        weaponRenderer.transform.localPosition = new Vector3(distance * Mathf.Lerp(1f, scaleFactor, 0.6f), 0f, 0f);
        weaponRenderer.transform.localScale = baseWeaponScale * scaleFactor;
        if (flipWhenAimingLeft) weaponRenderer.flipY = Mathf.Abs(angle) > 90f;

        if (playerRenderer != null)
        {
            weaponRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            // De espaldas (apuntando arriba) el arma queda detras del jugador.
            weaponRenderer.sortingOrder = playerRenderer.sortingOrder + (aim.Direction.y > 0.35f ? -1 : 1);
        }
    }

    /// Cuanto esta cargado el ataque (0 a 1): echa la espada hacia atras.
    public void SetCharge(float amount) => chargeAmount = Mathf.Clamp01(amount);

    /// Arco del arma durante el golpe, siguiendo la curva. Alterna el sentido en cada golpe.
    public void Swing(float duration)
    {
        if (swing != null) StopCoroutine(swing);
        swing = StartCoroutine(SwingRoutine(Mathf.Max(0.02f, duration)));
    }

    /// Swing del ataque cargado: la espada crece 'scaleMultiplier' veces y hace un arco mayor.
    public void SwingCharged(float duration, float scaleMultiplier)
    {
        if (swing != null) StopCoroutine(swing);
        swing = StartCoroutine(ChargedSwingRoutine(Mathf.Max(0.02f, duration), Mathf.Max(1f, scaleMultiplier)));
    }

    public void CancelSwing()
    {
        if (swing != null) StopCoroutine(swing);
        swing = null;
        swingOffset = 0f;
        chargeAmount = 0f;
        scaleFactor = 1f;
    }

    IEnumerator SwingRoutine(float duration)
    {
        float start = swingArc * 0.5f * swingSign;
        float end = -start;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;                 // con escala de tiempo: se congela con el hitstop
            float progress = swingCurve.Evaluate(Mathf.Clamp01(t / duration));
            swingOffset = Mathf.LerpUnclamped(start, end, progress);
            yield return null;
        }
        swingOffset = 0f;
        swingSign = -swingSign;
        swing = null;
    }

    IEnumerator ChargedSwingRoutine(float duration, float targetScale)
    {
        // Parte desde donde la espada estaba echada hacia atras y cruza al otro lado
        float start = chargePullAngle * swingSign;
        float end = -start * chargedArcMultiplier;
        chargeAmount = 0f;                       // desde ahora el angulo lo lleva swingOffset

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            swingOffset = Mathf.LerpUnclamped(start, end, swingCurve.Evaluate(k));
            scaleFactor = Mathf.LerpUnclamped(1f, targetScale, chargedScaleCurve.Evaluate(k));
            yield return null;
        }

        // Vuelve a su sitio y a su tamano
        float fromAngle = swingOffset;
        float fromScale = scaleFactor;
        const float back = 0.22f;
        t = 0f;
        while (t < back)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / back));
            swingOffset = Mathf.Lerp(fromAngle, 0f, k);
            scaleFactor = Mathf.Lerp(fromScale, 1f, k);
            yield return null;
        }

        swingOffset = 0f;
        scaleFactor = 1f;
        swingSign = -swingSign;
        swing = null;
    }
}
