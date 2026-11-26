using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// Luciernagas del menu: puntitos de luz que vuelan despacio dentro de un rectangulo, parpadean suave y, si el
/// prefab trae una luz (Point Light 2D), ILUMINAN los sprites que tengan material Sprite-Lit-Default.
/// El rectangulo se ve en la escena (gizmo amarillo) al seleccionar el objeto.
public class MenuFireflies : MonoBehaviour
{
    [Tooltip("Prefab de UNA luciernaga: SpriteRenderer con un puntito brillante y, como hijo, un Point Light 2D. " +
             "Vacio = se crean puntitos sin luz.")]
    [SerializeField] GameObject fireflyPrefab;
    [SerializeField, Min(0)] int count = 14;
    [Tooltip("Tamano del rectangulo donde vuelan (unidades), centrado en este objeto.")]
    [SerializeField] Vector2 area = new Vector2(18f, 8f);

    [Header("Vuelo")]
    [SerializeField] Vector2 speedRange = new Vector2(0.25f, 0.7f);
    [Tooltip("Que tanto cambian de direccion (mas = vuelo mas nervioso).")]
    [SerializeField] float wander = 0.6f;

    [Header("Brillo")]
    [Tooltip("Segundos de un parpadeo (min, max).")]
    [SerializeField] Vector2 blinkPeriod = new Vector2(1.6f, 3.4f);
    [Tooltip("Brillo minimo entre parpadeos (0 = se apagan del todo).")]
    [SerializeField, Range(0f, 1f)] float minGlow = 0.15f;
    [SerializeField] Vector2 sizeRange = new Vector2(0.12f, 0.22f);
    [SerializeField] Color color = new Color(1f, 0.9f, 0.45f, 1f);
    [Tooltip("Orden de dibujo de los puntitos (min, max). Pon un rango que quede entre capas: unas delante y otras detras.")]
    [SerializeField] Vector2Int sortingOrderRange = new Vector2Int(5, 30);
    [SerializeField] string sortingLayerName = "Default";

    [Header("Aparecer")]
    [SerializeField] float fadeInDelay = 1.2f;
    [SerializeField] float fadeInTime = 2.5f;

    class Fly
    {
        public Transform t;
        public SpriteRenderer sr;
        public Light2D light;
        public float lightBase;
        public Vector2 pos, dir;
        public float speed, period, phase, size, noise;
    }

    readonly List<Fly> flies = new List<Fly>();
    static Sprite glowSprite;
    float startTime;
    float alive;

    void Start()
    {
        startTime = Time.unscaledTime;
        for (int i = 0; i < count; i++) flies.Add(Spawn(i));
    }

    Fly Spawn(int i)
    {
        GameObject go;
        if (fireflyPrefab != null) go = Instantiate(fireflyPrefab, transform);
        else
        {
            go = new GameObject("Firefly");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = GlowSprite();
            var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh != null) r.sharedMaterial = new Material(sh);
        }
        go.name = "Firefly " + i;

        var f = new Fly
        {
            t = go.transform,
            sr = go.GetComponentInChildren<SpriteRenderer>(),
            light = go.GetComponentInChildren<Light2D>(),
            pos = new Vector2(Random.Range(-0.5f, 0.5f) * area.x, Random.Range(-0.5f, 0.5f) * area.y),
            dir = Random.insideUnitCircle.normalized,
            speed = Random.Range(speedRange.x, speedRange.y),
            period = Random.Range(blinkPeriod.x, blinkPeriod.y),
            phase = Random.value * 10f,
            size = Random.Range(sizeRange.x, sizeRange.y),
            noise = Random.value * 100f
        };
        if (f.light != null) f.lightBase = f.light.intensity;
        if (f.sr != null)
        {
            if (f.sr.sprite == null) f.sr.sprite = GlowSprite();
            f.sr.sortingLayerName = sortingLayerName;
            f.sr.sortingOrder = Random.Range(sortingOrderRange.x, sortingOrderRange.y + 1);
        }
        return f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;
        alive += Mathf.Min(dt, 1f / 20f);       // tiempo propio: no salta con el tiron de carga de la escena
        float appear = fadeInTime > 0f ? Mathf.Clamp01((alive - fadeInDelay) / fadeInTime) : 1f;
        Vector2 half = area * 0.5f;

        foreach (var f in flies)
        {
            if (f.t == null) continue;

            // Vuelo: la direccion gira con ruido suave; si se sale del rectangulo, gira hacia adentro
            float turn = (Mathf.PerlinNoise(f.noise, t * 0.35f) - 0.5f) * 2f * wander * Mathf.PI * dt * 2f;
            f.dir = Rotate(f.dir, turn);
            if (Mathf.Abs(f.pos.x) > half.x || Mathf.Abs(f.pos.y) > half.y)
                f.dir = Vector2.Lerp(f.dir, -f.pos.normalized, dt * 2f).normalized;
            f.pos += f.dir * f.speed * dt;
            f.t.localPosition = new Vector3(f.pos.x, f.pos.y + Mathf.Sin(t * 1.7f + f.phase) * 0.08f, 0f);

            // Parpadeo suave
            float blink = 0.5f + 0.5f * Mathf.Sin((t + f.phase) * Mathf.PI * 2f / f.period);
            blink = Mathf.Lerp(minGlow, 1f, blink * blink) * appear;

            if (f.sr != null)
            {
                Color c = color;
                c.a = color.a * blink;
                f.sr.color = c;
                f.t.localScale = Vector3.one * f.size * Mathf.Lerp(0.7f, 1f, blink) / Mathf.Max(0.0001f, SpriteSize(f.sr));
            }
            if (f.light != null) f.light.intensity = f.lightBase * blink;
        }
    }

    static float SpriteSize(SpriteRenderer sr) => sr.sprite != null ? Mathf.Max(sr.sprite.bounds.size.x, 0.0001f) : 1f;

    static Vector2 Rotate(Vector2 v, float radians)
    {
        float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // Puntito brillante (centro solido y borde difuso) por si no hay sprite
    static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d);
                a = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.35f - d) / 0.12f)), a * a * 0.6f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return glowSprite;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.8f);
        Gizmos.DrawWireCube(transform.position, new Vector3(area.x, area.y, 0f));
    }
}
