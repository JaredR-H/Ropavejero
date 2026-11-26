using UnityEngine;
using UnityEngine.Rendering.Universal;

/// Luz 2D que respira y titila suave (por ejemplo, la luz calida que sale de la puerta del menu).
/// Va en el mismo objeto que el Light 2D.
[RequireComponent(typeof(Light2D))]
public class MenuLightFlicker : MonoBehaviour
{
    [Tooltip("Variacion lenta de la intensidad (fraccion: 0.15 = +-15%).")]
    [SerializeField, Range(0f, 1f)] float breathe = 0.12f;
    [SerializeField, Min(0.1f)] float breathePeriod = 4f;
    [Tooltip("Titileo rapido tipo vela/fuego (fraccion).")]
    [SerializeField, Range(0f, 1f)] float flicker = 0.06f;
    [SerializeField] float flickerSpeed = 9f;
    [Tooltip("Variacion del radio (fraccion). Da la sensacion de que la luz 'late'.")]
    [SerializeField, Range(0f, 0.5f)] float radiusPulse = 0.04f;

    [Header("Aparecer")]
    [Tooltip("Segundos de espera y de subida de la luz al empezar la escena (0 = ya encendida).")]
    [SerializeField] float fadeInDelay = 0.3f;
    [SerializeField] float fadeInTime = 2f;

    Light2D light2D;
    float baseIntensity, baseOuter, startTime, seed, alive;

    void Awake()
    {
        light2D = GetComponent<Light2D>();
        baseIntensity = light2D.intensity;
        baseOuter = light2D.pointLightOuterRadius;
        seed = Random.value * 50f;
        startTime = Time.unscaledTime;
    }

    void Update()
    {
        float t = Time.unscaledTime;
        alive += Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);   // no salta con el tiron de carga de la escena
        float appear = fadeInTime > 0f ? Mathf.Clamp01((alive - fadeInDelay) / fadeInTime) : 1f;
        appear = appear * appear * (3f - 2f * appear);

        float slow = Mathf.Sin(t * Mathf.PI * 2f / breathePeriod) * breathe;
        float fast = (Mathf.PerlinNoise(seed, t * flickerSpeed) - 0.5f) * 2f * flicker;
        light2D.intensity = baseIntensity * (1f + slow + fast) * appear;

        if (radiusPulse > 0f && light2D.lightType == Light2D.LightType.Point)
            light2D.pointLightOuterRadius = baseOuter * (1f + Mathf.Sin(t * Mathf.PI * 2f / breathePeriod + 1f) * radiusPulse);
    }
}
