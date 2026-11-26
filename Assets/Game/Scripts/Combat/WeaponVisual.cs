using System.Collections;
using UnityEngine;

public class WeaponVisual : MonoBehaviour
{
    [SerializeField] PlayerAim aim;
    [Tooltip("El SpriteRenderer del arma (hijo de este objeto).")]
    [SerializeField] SpriteRenderer weaponRenderer;
    [Tooltip("El SpriteRenderer del jugador, para ordenar el arma delante o detras de el.")]
    [SerializeField] SpriteRenderer playerRenderer;
    [Tooltip("Radio de la orbita: que tan lejos del centro del jugador gira el arma. Mayor = el baston rota a su alrededor.")]
    [SerializeField] float distance = 0.8f;
    [Tooltip("Angulo total del arco al golpear.")]
    [SerializeField] float swingArc = 140f;
    [Tooltip("Voltea el sprite en vertical al apuntar a la izquierda para que no quede de cabeza.")]
    [SerializeField] bool flipWhenAimingLeft = true;

    [Header("Orden de dibujo")]
    [Tooltip("Marcado: el arma SIEMPRE se dibuja delante del jugador (tambien de espaldas).")]
    [SerializeField] bool alwaysInFront = true;
    [Tooltip("Cuantos ordenes por encima del jugador va el arma (la estela va uno por debajo del arma).")]
    [SerializeField, Min(1)] int sortingOffset = 2;

    [Header("Swing pesado (un baston mas grande que el nino)")]
    [Tooltip("Cuanto se echa hacia atras ANTES del golpe, ademas de medio arco (fraccion del arco). Mas = mas esfuerzo.")]
    [SerializeField, Range(0f, 0.5f)] float anticipation = 0.15f;
    [Tooltip("Cuanto se pasa de largo al terminar el golpe (fraccion del arco): el peso lo arrastra.")]
    [SerializeField, Range(0f, 0.5f)] float overshoot = 0.12f;
    [Tooltip("Echarse atras (Wind Up del PlayerAttack): tiempo (0 a 1) en X, avance (0 a 1) en Y. Lento al principio = pesado.")]
    [SerializeField] AnimationCurve windUpCurve = DefaultWindUpCurve();
    [Tooltip("El golpe (Active Time del PlayerAttack): tiempo (0 a 1) en X, avance (0 a 1) en Y. Arranca lento y acelera.")]
    [SerializeField] AnimationCurve strikeCurve = DefaultStrikeCurve();
    [Tooltip("Segundos que tarda en asentarse despues de pasarse de largo (inercia del peso).")]
    [SerializeField, Min(0f)] float settleTime = 0.18f;
    [Tooltip("Segundos que tarda en volver a su posicion de reposo despues de asentarse.")]
    [SerializeField, Min(0f)] float returnTime = 0.22f;

    [Header("Estela del swing")]
    [SerializeField] bool useTrail = true;

    [Header("Aparecer / desaparecer")]
    [Tooltip("Segundos que tarda el arma en aparecer o guardarse.")]
    [SerializeField] float appearTime = 0.18f;
    [Tooltip("Tamano del arma al aparecer: tiempo (0 a 1) en X, tamano (0 a 1) en Y. Pasarse de 1 da un pequeno rebote.")]
    [SerializeField] AnimationCurve appearCurve = DefaultAppearCurve();

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
    bool shown = true;
    float showT = 1f;

    static AnimationCurve DefaultAppearCurve()
    {
        var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.65f, 1.18f), new Keyframe(1f, 1f));
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
        return curve;
    }

    /// Muestra u oculta el arma (con un pequeno rebote al aparecer).
    public void SetVisible(bool visible, bool instant = false)
    {
        if (visible && !shown && !instant) Sfx.Play(SfxId.WeaponAppear, transform.position);
        shown = visible;
        if (instant) showT = visible ? 1f : 0f;
        if (!visible) CancelSwing();
    }

    // Se echa atras lento (le cuesta levantarlo) y se frena arriba
    static AnimationCurve DefaultWindUpCurve()
    {
        return new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.6f), new Keyframe(0.7f, 0.9f, 1.2f, 1.2f), new Keyframe(1f, 1f, 0f, 0f));
    }

    // El golpe arranca lento (el peso) y acelera hasta el final
    static AnimationCurve DefaultStrikeCurve()
    {
        return new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.2f), new Keyframe(0.45f, 0.3f, 1.4f, 1.4f), new Keyframe(1f, 1f, 1.9f, 0f));
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

    SwingTrail trail;

    void Awake()
    {
        if (aim == null) aim = GetComponentInParent<PlayerAim>();
        if (weaponRenderer != null) baseWeaponScale = weaponRenderer.transform.localScale;
        if (useTrail && weaponRenderer != null) trail = SwingTrail.For(weaponRenderer);
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

        // Aparecer / guardarse
        showT = Mathf.MoveTowards(showT, shown ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, appearTime));
        float pop = shown ? appearCurve.Evaluate(showT) : showT * showT;
        weaponRenderer.enabled = showT > 0.001f;

        weaponRenderer.transform.localPosition = new Vector3(distance * Mathf.Lerp(1f, scaleFactor, 0.6f), 0f, 0f);
        weaponRenderer.transform.localScale = baseWeaponScale * scaleFactor * pop;
        if (flipWhenAimingLeft) weaponRenderer.flipY = Mathf.Abs(angle) > 90f;

        if (playerRenderer != null)
        {
            weaponRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            // Por defecto siempre delante; sin 'Always In Front', de espaldas (apuntando arriba) queda detras.
            bool behind = !alwaysInFront && aim.Direction.y > 0.35f;
            weaponRenderer.sortingOrder = playerRenderer.sortingOrder + (behind ? -sortingOffset : sortingOffset);
        }
    }

    /// Cuanto esta cargado el ataque (0 a 1): echa la espada hacia atras.
    public void SetCharge(float amount) => chargeAmount = Mathf.Clamp01(amount);

    /// Golpe pesado: se echa atras durante 'windUp', golpea durante 'activeTime' (con estela), se pasa de largo,
    /// se asienta y vuelve. Usa los mismos tiempos que el hitbox del PlayerAttack. Alterna el sentido en cada golpe.
    public void Swing(float windUp, float activeTime)
    {
        if (swing != null) StopCoroutine(swing);
        swing = StartCoroutine(SwingRoutine(Mathf.Max(0f, windUp), Mathf.Max(0.02f, activeTime)));
    }

    /// Compatibilidad: reparte la duracion entre echarse atras (40%) y golpear (60%).
    public void Swing(float duration) => Swing(duration * 0.4f, duration * 0.6f);

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
        if (trail != null) trail.Clear();
    }

    IEnumerator SwingRoutine(float windUp, float activeTime)
    {
        int sign = swingSign;
        swingSign = -swingSign;                  // el siguiente golpe va al reves (aunque este se interrumpa)

        float from = swingOffset;                // si venia de otro golpe, sigue desde donde estaba
        float back = (swingArc * 0.5f + swingArc * anticipation) * sign;
        float end = -swingArc * 0.5f * sign;
        float past = end - swingArc * overshoot * sign;

        // 1) Se echa hacia atras con esfuerzo
        yield return Animate(windUp, k => swingOffset = Mathf.LerpUnclamped(from, back, windUpCurve.Evaluate(k)));

        // 2) Golpe: arranca lento, acelera y se pasa de largo (con estela)
        if (trail != null) trail.Begin();
        yield return Animate(activeTime, k => swingOffset = Mathf.LerpUnclamped(back, past, strikeCurve.Evaluate(k)));
        if (trail != null) trail.End();

        // 3) La inercia lo deja un poco pasado y se asienta
        yield return Animate(settleTime, k => swingOffset = Mathf.Lerp(past, end, Mathf.SmoothStep(0f, 1f, k)));

        // 4) Vuelve a reposo
        yield return Animate(returnTime, k => swingOffset = Mathf.Lerp(end, 0f, Mathf.SmoothStep(0f, 1f, k)));

        swingOffset = 0f;
        swing = null;
    }

    // Recorre 'duration' segundos (con escala de tiempo: se congela con el hitstop) llamando a step(0..1)
    static IEnumerator Animate(float duration, System.Action<float> step)
    {
        if (duration <= 0f)
        {
            step(1f);
            yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            step(Mathf.Clamp01(t / duration));
            yield return null;
        }
    }

    IEnumerator ChargedSwingRoutine(float duration, float targetScale)
    {
        // Parte desde donde la espada estaba echada hacia atras y cruza al otro lado
        float start = chargePullAngle * swingSign;
        float end = -start * chargedArcMultiplier;
        chargeAmount = 0f;                       // desde ahora el angulo lo lleva swingOffset

        if (trail != null) trail.Begin();
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            swingOffset = Mathf.LerpUnclamped(start, end, strikeCurve.Evaluate(k));
            scaleFactor = Mathf.LerpUnclamped(1f, targetScale, chargedScaleCurve.Evaluate(k));
            yield return null;
        }
        if (trail != null) trail.End();

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
