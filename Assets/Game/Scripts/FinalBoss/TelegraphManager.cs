using System.Collections.Generic;
using UnityEngine;


public class TelegraphManager : MonoBehaviour
{
    [Header("Aspecto")]
    [SerializeField] Color color = new Color(1f, 0.2f, 0.2f, 1f);
    [Tooltip("Transparencia minima y maxima del parpadeo.")]
    [SerializeField, Range(0f, 1f)] float minAlpha = 0.18f;
    [SerializeField, Range(0f, 1f)] float maxAlpha = 0.6f;
    [Tooltip("Parpadeos por segundo al aparecer y justo antes del golpe (se acelera).")]
    [SerializeField] float blinkStart = 3f;
    [SerializeField] float blinkEnd = 14f;
    [Tooltip("Sorting Layer del suelo/personajes. Pon un Order mayor que el del suelo y menor que el de los personajes.")]
    [SerializeField] string sortingLayerName = "Default";
    [SerializeField] int sortingOrder = 1;

    static TelegraphManager instance;
    static bool quitting;

    readonly List<TelegraphHandle> handles = new List<TelegraphHandle>();
    Sprite square;
    Sprite disc;

    public static TelegraphManager Get()
    {
        if (instance != null || quitting) return instance;
        instance = FindFirstObjectByType<TelegraphManager>();
        if (instance == null) instance = new GameObject("TelegraphManager").AddComponent<TelegraphManager>();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        BuildSprites();
    }

    void OnApplicationQuit() => quitting = true;

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public TelegraphHandle ShowRect(Vector2 origin, Vector2 direction, float length, float width, float duration)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction.Normalize();
        TelegraphHandle h = Take(square);
        Transform t = h.sr.transform;
        t.position = origin + direction * (length * 0.5f);
        t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        t.localScale = new Vector3(length, width, 1f);
        Begin(h, duration);
        return h;
    }

    public TelegraphHandle ShowCircle(Vector2 center, float radius, float duration)
    {
        TelegraphHandle h = Take(disc);
        Transform t = h.sr.transform;
        t.position = center;
        t.rotation = Quaternion.identity;
        t.localScale = Vector3.one * (radius * 2f);
        Begin(h, duration);
        return h;
    }

    void Begin(TelegraphHandle h, float duration)
    {
        h.elapsed = 0f;
        h.duration = Mathf.Max(0.05f, duration);
        h.alive = true;
        h.sr.enabled = true;
        h.sr.sortingLayerName = sortingLayerName;
        h.sr.sortingOrder = sortingOrder;
        Apply(h);
    }

    TelegraphHandle Take(Sprite sprite)
    {
        TelegraphHandle h = null;
        foreach (var x in handles)
            if (!x.alive) { h = x; break; }

        if (h == null)
        {
            var go = new GameObject("Telegraph");
            go.transform.SetParent(transform, false);
            h = new TelegraphHandle { sr = go.AddComponent<SpriteRenderer>() };
            handles.Add(h);
        }
        h.sr.sprite = sprite;
        return h;
    }

    void Update()
    {
        float dt = Time.deltaTime;      // con escala de tiempo: se congela con el hitstop
        foreach (var h in handles)
        {
            if (!h.alive) continue;
            h.elapsed += dt;
            if (h.elapsed >= h.duration)
            {
                h.Hide();
                continue;
            }
            Apply(h);
        }
    }

    // Parpadeo que se acelera a medida que se acerca el golpe
    void Apply(TelegraphHandle h)
    {
        float k = Mathf.Clamp01(h.elapsed / h.duration);
        float freq = Mathf.Lerp(blinkStart, blinkEnd, k * k);
        float phase = 0.5f + 0.5f * Mathf.Sin(h.elapsed * freq * Mathf.PI * 2f);
        Color c = color;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, phase);
        h.sr.color = c;
    }

    void BuildSprites()
    {
        // Cuadrado blanco de 1 x 1 unidad
        var sq = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        sq.SetPixels(px);
        sq.Apply();
        square = Sprite.Create(sq, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        square.hideFlags = HideFlags.HideAndDontSave;

        // Circulo de 1 unidad de diametro, con el borde un poco mas marcado
        const int size = 128;
        var dt = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        float r = (size - 2) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - size * 0.5f + 0.5f) * (x - size * 0.5f + 0.5f) + (y - size * 0.5f + 0.5f) * (y - size * 0.5f + 0.5f)) / r;
                float a = d > 1f ? 0f : (d > 0.9f ? 1f : 0.7f);
                a *= Mathf.Clamp01((1f - d) * r);        // borde suavizado
                dt.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        dt.Apply();
        disc = Sprite.Create(dt, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        disc.hideFlags = HideFlags.HideAndDontSave;
    }
}
