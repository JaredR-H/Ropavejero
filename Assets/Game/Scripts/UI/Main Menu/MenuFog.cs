using UnityEngine;

/// Niebla del menu: el sprite se mece de lado a lado despacio, respira (sube y baja su opacidad) y puede deslizarse
/// en bucle. Pon varias copias del sprite de humo con distinta velocidad, tamano, color y orden: una detras del jefe
/// y otra delante del personaje, a ras de suelo.
[RequireComponent(typeof(SpriteRenderer))]
public class MenuFog : MonoBehaviour
{
    [Tooltip("Cuanto se mece de lado a lado (unidades).")]
    [SerializeField] float swayDistance = 0.6f;
    [Tooltip("Segundos de un vaiven completo.")]
    [SerializeField, Min(0.1f)] float swayPeriod = 14f;
    [Tooltip("Subida y bajada vertical (unidades).")]
    [SerializeField] float bobDistance = 0.08f;
    [Tooltip("Opacidad minima y maxima mientras respira.")]
    [SerializeField] Vector2 alphaRange = new Vector2(0.55f, 0.85f);
    [Tooltip("Segundos de una respiracion completa.")]
    [SerializeField, Min(0.1f)] float breathePeriod = 7f;
    [Tooltip("Desfase para que dos nieblas no se muevan igual (0 a 1).")]
    [SerializeField, Range(0f, 1f)] float phase = 0f;

    /// Visibilidad general (0 a 1). La usa MainMenuIntro para hacer aparecer la niebla.
    public float Visibility { get; set; } = 1f;

    SpriteRenderer sr;
    Vector3 basePos;
    Color baseColor;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        basePos = transform.localPosition;
        baseColor = sr.color;
    }

    /// Desplazamiento extra (lo usa la intro para que el humo entre desde un lado).
    public Vector3 ExtraOffset { get; set; }

    void LateUpdate()
    {
        float t = Time.unscaledTime;
        float p = phase * Mathf.PI * 2f;
        float sway = Mathf.Sin(t * Mathf.PI * 2f / swayPeriod + p) * swayDistance;
        float bob = Mathf.Sin(t * Mathf.PI * 2f / (swayPeriod * 0.63f) + p * 1.7f) * bobDistance;
        transform.localPosition = basePos + new Vector3(sway, bob, 0f) + ExtraOffset;

        float breathe = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / breathePeriod + p * 2.3f);
        Color c = baseColor;
        c.a = baseColor.a * Mathf.Lerp(alphaRange.x, alphaRange.y, breathe) * Mathf.Clamp01(Visibility);
        sr.color = c;
    }
}
