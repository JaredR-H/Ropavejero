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

    float swingOffset;
    int swingSign = 1;
    Coroutine swing;

    static AnimationCurve DefaultSwingCurve()
    {
        var curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.18f, -0.12f),
            new Keyframe(0.5f, 1.08f),
            new Keyframe(0.78f, 0.98f),
            new Keyframe(1f, 1f));
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
        return curve;
    }

    void Awake()
    {
        if (aim == null) aim = GetComponentInParent<PlayerAim>();
    }

    void LateUpdate()
    {
        if (aim == null) return;

        float angle = aim.Angle;
        transform.position = aim.Origin;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + swingOffset);

        if (weaponRenderer == null) return;

        weaponRenderer.transform.localPosition = new Vector3(distance, 0f, 0f);
        if (flipWhenAimingLeft) weaponRenderer.flipY = Mathf.Abs(angle) > 90f;

        if (playerRenderer != null)
        {
            weaponRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            weaponRenderer.sortingOrder = playerRenderer.sortingOrder + (aim.Direction.y > 0.35f ? -1 : 1);
        }
    }
    public void Swing(float duration)
    {
        if (swing != null) StopCoroutine(swing);
        swing = StartCoroutine(SwingRoutine(Mathf.Max(0.02f, duration)));
    }

    public void CancelSwing()
    {
        if (swing != null) StopCoroutine(swing);
        swing = null;
        swingOffset = 0f;
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
}
