using System.Collections.Generic;
using UnityEngine;

/// Tipos de efecto que se pueden pedir desde cualquier script con Vfx.Play(...).
public enum VfxKind
{
    Dust,        // polvo / humo: pasos, dash, flechas al salir, puertas, trampas que desaparecen
    HitEnemy,    // golpe a un enemigo: chispas blancas + destello
    HitPlayer,   // golpe al jugador: chispas rojas + destello
    Impact,      // flecha o proyectil que choca con una pared o con alguien
    Explosion,   // explosion del enemigo de area y del jefe
    Stars,       // estrellitas sueltas: botones al pisarlos
    Poof         // nube grande al derrotar a un enemigo o al jefe (mas grande que el personaje)
}

/// Atajo para lanzar efectos: Vfx.Play(VfxKind.Dust, posicion, direccion, escala).
public static class Vfx
{
    public static void Play(VfxKind kind, Vector2 position, Vector2 direction = default, float scale = 1f)
    {
        VfxManager m = VfxManager.Get();
        if (m != null) m.Play(kind, position, direction, scale);
    }
}

/// Crea y recicla los sistemas de particulas del juego. Si no hay uno en la escena se crea solo con efectos por defecto
/// (hechos por codigo), pero conviene ponerlo en la escena Game para ajustar sus valores o usar tus propios prefabs.
public class VfxManager : MonoBehaviour
{
    [System.Serializable]
    public class Override
    {
        public VfxKind kind;
        [Tooltip("Tu propio prefab de particulas (Play On Awake desmarcado, Simulation Space = World).")]
        public ParticleSystem prefab;
    }

    [System.Serializable]
    public class LayerRule
    {
        public VfxKind kind = VfxKind.Dust;
        [Tooltip("Sorting Layer para este tipo (debe existir en Tags and Layers).")]
        public string sortingLayerName = "Default";
        [Tooltip("Menor que el Order in Layer de los personajes = se dibuja DETRAS de ellos.")]
        public int sortingOrder = 2;
    }

    [Header("Dibujos de las particulas (blancos sobre fondo transparente)")]
    [Tooltip("NUBE: polvo de pasos/dash, puertas, flechas al salir y al chocar, trampas, humo de la explosion y nube de derrota.")]
    [SerializeField] Texture2D smokeTexture;
    [Tooltip("Si el dibujo de humo trae varias variaciones en cuadricula (por ejemplo 2 x 2), pon aqui columnas y filas. 1 x 1 = un solo dibujo.")]
    [SerializeField] Vector2Int smokeSheet = Vector2Int.one;
    [Tooltip("Chispas de los golpes, impactos y explosion. Dibujala simetrica (estrella o rombo): el codigo la estira en la direccion en que vuela.")]
    [SerializeField] Texture2D sparkTexture;
    [SerializeField] Vector2Int sparkSheet = Vector2Int.one;
    [Tooltip("ESTRELLA: golpes a enemigos, golpes al jugador y botones. Gira y no se estira. Vacio = usa el dibujo de chispa.")]
    [SerializeField] Texture2D starTexture;
    [SerializeField] Vector2Int starSheet = Vector2Int.one;
    [Tooltip("Destello del golpe y de la explosion (un circulo o estallido).")]
    [SerializeField] Texture2D flashTexture;
    [Tooltip("Opcional: bolas de fuego de la explosion. Vacio = usa el dibujo de humo.")]
    [SerializeField] Texture2D fireTexture;
    [SerializeField] Vector2Int fireSheet = Vector2Int.one;

    [Header("Materiales")]
    [Tooltip("Material base de las particulas: shader 'Universal Render Pipeline/Particles/Unlit' (Surface Type Transparent, " +
             "Blending Alpha) o 'Sprite-Unlit-Default'. NUNCA Sprite-LIT (sin luces 2D se ve negro). " +
             "Deja su textura vacia: el codigo pone el dibujo de cada efecto. Vacio = se crea uno solo.")]
    [SerializeField] Material particleMaterial;
    [Tooltip("Opcional: material que brilla (Particles/Unlit con Blending Additive) para chispas y destellos. Vacio = el material base.")]
    [SerializeField] Material glowMaterial;
    [Tooltip("Sorting Layer de las particulas (debe existir). Ponlas por encima de los personajes.")]
    [SerializeField] string sortingLayerName = "Default";
    [SerializeField] int sortingOrder = 50;
    [Tooltip("Capa propia para algunos tipos. Los que no esten en la lista usan la capa de arriba (al frente). " +
             "Por defecto el polvo y los impactos van DETRAS de los personajes.")]
    [SerializeField] LayerRule[] layerRules =
    {
        new LayerRule { kind = VfxKind.Dust, sortingLayerName = "Default", sortingOrder = 2 },
        new LayerRule { kind = VfxKind.Impact, sortingLayerName = "Default", sortingOrder = 2 },
    };
    [Tooltip("Multiplica el tamano de TODOS los efectos.")]
    [SerializeField, Min(0.1f)] float globalScale = 1f;

    [Header("Tamano por tipo de efecto (multiplica la escala que pide cada script)")]
    [SerializeField, Min(0f)] float dustSize = 1f;
    [SerializeField, Min(0f)] float hitEnemySize = 1f;
    [SerializeField, Min(0f)] float hitPlayerSize = 1f;
    [SerializeField, Min(0f)] float impactSize = 1f;
    [SerializeField, Min(0f)] float explosionSize = 1f;
    [SerializeField, Min(0f)] float starsSize = 1f;
    [SerializeField, Min(0f)] float poofSize = 1f;

    [Header("Colores por defecto")]
    [SerializeField] Color dustColor = new Color(0.86f, 0.8f, 0.7f, 0.85f);
    [SerializeField] Color enemyHitColor = Color.white;
    [SerializeField] Color playerHitColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] Color explosionColor = new Color(1f, 0.75f, 0.25f);
    [SerializeField] Color starColor = new Color(1f, 0.93f, 0.55f);
    [SerializeField] Color poofColor = new Color(0.95f, 0.93f, 0.9f, 1f);

    [Header("Tus propios prefabs (opcional)")]
    [Tooltip("Si pones un prefab para un tipo, se usa en vez del efecto por defecto.")]
    [SerializeField] Override[] overrides;
    [Tooltip("Cuantas copias de cada efecto puede haber a la vez.")]
    [SerializeField, Range(4, 64)] int maxPerKind = 24;

    static VfxManager instance;
    static bool quitting;

    readonly Dictionary<VfxKind, List<ParticleSystem>> pools = new Dictionary<VfxKind, List<ParticleSystem>>();
    readonly Dictionary<VfxKind, int> nextIndex = new Dictionary<VfxKind, int>();
    Material material;
    Texture2D softDot;

    public static VfxManager Get()
    {
        if (instance != null || quitting) return instance;
        instance = FindFirstObjectByType<VfxManager>();
        if (instance == null) instance = new GameObject("VfxManager").AddComponent<VfxManager>();
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
        SetupMaterial();
    }

    void OnApplicationQuit() => quitting = true;

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ---------- Reproducir ----------
    public void Play(VfxKind kind, Vector2 position, Vector2 direction, float scale)
    {
        if (material == null) SetupMaterial();
        ParticleSystem ps = Take(kind);
        if (ps == null) return;

        Transform t = ps.transform;
        t.position = new Vector3(position.x, position.y, 0f);
        float angle = direction.sqrMagnitude > 0.0001f ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg : 0f;
        t.rotation = Quaternion.Euler(0f, 0f, angle);
        t.localScale = Vector3.one * Mathf.Max(0.05f, scale * globalScale * SizeFor(kind));

        ps.Clear(true);
        ps.Play(true);
    }

    float SizeFor(VfxKind kind)
    {
        switch (kind)
        {
            case VfxKind.Dust: return dustSize;
            case VfxKind.HitEnemy: return hitEnemySize;
            case VfxKind.HitPlayer: return hitPlayerSize;
            case VfxKind.Impact: return impactSize;
            case VfxKind.Explosion: return explosionSize;
            case VfxKind.Stars: return starsSize;
            case VfxKind.Poof: return poofSize;
        }
        return 1f;
    }

    ParticleSystem Take(VfxKind kind)
    {
        if (!pools.TryGetValue(kind, out var list))
        {
            list = new List<ParticleSystem>();
            pools[kind] = list;
            nextIndex[kind] = 0;
        }

        foreach (var p in list)
            if (p != null && !p.IsAlive(true)) return p;

        if (list.Count < maxPerKind)
        {
            ParticleSystem created = Create(kind);
            if (created != null) ApplyLayer(kind, created);
            if (created != null) list.Add(created);
            return created;
        }

        // Todos ocupados: se recicla el mas viejo
        int i = nextIndex[kind] % list.Count;
        nextIndex[kind] = i + 1;
        return list[i];
    }

    void ApplyLayer(VfxKind kind, ParticleSystem ps)
    {
        if (layerRules == null) return;
        foreach (var rule in layerRules)
        {
            if (rule == null || rule.kind != kind) continue;
            int i = 0;
            foreach (var r in ps.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (!string.IsNullOrEmpty(rule.sortingLayerName)) r.sortingLayerName = rule.sortingLayerName;
                r.sortingOrder = rule.sortingOrder + i++;      // los sub-efectos quedan uno encima del otro
            }
            return;
        }
    }

    ParticleSystem Create(VfxKind kind)
    {
        if (overrides != null)
            foreach (var o in overrides)
                if (o != null && o.kind == kind && o.prefab != null)
                {
                    ParticleSystem copy = Instantiate(o.prefab, transform);
                    copy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    return copy;
                }

        switch (kind)
        {
            case VfxKind.Dust: return BuildDust();
            case VfxKind.HitEnemy: return BuildHit("HitEnemy", enemyHitColor, 1f);
            case VfxKind.HitPlayer: return BuildHit("HitPlayer", playerHitColor, 1.3f);
            case VfxKind.Impact: return BuildImpact();
            case VfxKind.Explosion: return BuildExplosion();
            case VfxKind.Stars: return BuildStars();
            case VfxKind.Poof: return BuildPoof();
        }
        return null;
    }

    // ---------- Efectos por defecto ----------
    ParticleSystem BuildDust()
    {
        ParticleSystem ps = MakeSystem("Dust", transform);
        var main = ps.main;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.26f, 0.45f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = dustColor;
        main.gravityModifier = -0.04f;                                  // sube un poquito, como el humo

        Burst(ps, 4, 6);
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.15f;

        Slowdown(ps, 0.07f);
        PopAndShrink(ps, dustColor);
        return ps;
    }

    ParticleSystem BuildHit(string label, Color color, float size)
    {
        // Estrellas que salen disparadas hacia donde va el golpe, girando
        ParticleSystem stars = MakeSystem(label, transform, Look.Star);
        var main = stars.main;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f * size, 6.5f * size);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f * size, 0.28f * size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;
        Burst(stars, 5, 7);
        ConeAlongX(stars, 50f);
        Slowdown(stars, 0.14f);
        Spin(stars, 540f);
        PopAndShrink(stars, color);

        // Destello redondo en el punto del golpe
        ParticleSystem flash = MakeSystem("Flash", stars.transform, Look.Flash);
        var fm = flash.main;
        fm.duration = 0.1f;
        fm.startLifetime = 0.09f;
        fm.startSpeed = 0f;
        fm.startSize = 0.9f * size;
        fm.startColor = color;
        Burst(flash, 1, 1);
        GrowAndFade(flash, 0.6f, 1.3f, color);
        return stars;
    }

    ParticleSystem BuildImpact()
    {
        // Flecha o proyectil que choca: nubecitas que rebotan hacia atras
        ParticleSystem dust = MakeSystem("Impact", transform);
        var dm = dust.main;
        dm.duration = 0.2f;
        dm.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        dm.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        dm.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
        dm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        dm.startColor = dustColor;
        Burst(dust, 3, 5);
        ConeAlongX(dust, 65f);
        Slowdown(dust, 0.12f);
        PopAndShrink(dust, dustColor);
        return dust;
    }

    ParticleSystem BuildStars()
    {
        // Botones: estrellitas que saltan hacia arriba y a los lados
        ParticleSystem stars = MakeSystem("Stars", transform, Look.Star);
        var main = stars.main;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = starColor;
        main.gravityModifier = 0.35f;                                   // caen un poco despues de saltar
        Burst(stars, 4, 6);
        var shape = stars.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;
        shape.radiusThickness = 0f;                                     // salen desde el borde hacia afuera
        Slowdown(stars, 0.1f);
        Spin(stars, 420f);
        PopAndShrink(stars, starColor);
        return stars;
    }

    ParticleSystem BuildPoof()
    {
        // Nube grande de derrota: anillo de nubes que se abre y una nube central
        ParticleSystem ring = MakeSystem("Poof", transform);
        var main = ring.main;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 0.85f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = poofColor;
        main.gravityModifier = -0.05f;
        Burst(ring, 8, 10);
        var shape = ring.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.35f;
        shape.radiusThickness = 0f;
        Slowdown(ring, 0.16f);
        PopAndShrink(ring, poofColor);

        ParticleSystem core = MakeSystem("Core", ring.transform);
        var cm = core.main;
        cm.duration = 0.1f;
        cm.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.55f);
        cm.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        cm.startSize = new ParticleSystem.MinMaxCurve(1.1f, 1.4f);
        cm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        cm.startColor = poofColor;
        Burst(core, 2, 3);
        PopAndShrink(core, poofColor);

        // Unas estrellitas que salen volando
        ParticleSystem stars = MakeSystem("Stars", ring.transform, Look.Star);
        var sm = stars.main;
        sm.duration = 0.1f;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
        sm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        sm.startColor = starColor;
        Burst(stars, 4, 6);
        var ss = stars.shape;
        ss.enabled = true;
        ss.shapeType = ParticleSystemShapeType.Circle;
        ss.radius = 0.3f;
        ss.radiusThickness = 0f;
        Slowdown(stars, 0.12f);
        Spin(stars, 480f);
        PopAndShrink(stars, starColor);
        return ring;
    }

    ParticleSystem BuildExplosion()
    {
        // Destello grande
        ParticleSystem flash = MakeSystem("Explosion", transform, Look.Flash);
        var fm = flash.main;
        fm.duration = 0.15f;
        fm.startLifetime = 0.14f;
        fm.startSpeed = 0f;
        fm.startSize = 3.2f;
        fm.startColor = new Color(1f, 0.95f, 0.7f);
        Burst(flash, 1, 1);
        GrowAndFade(flash, 0.5f, 1.2f, new Color(1f, 0.95f, 0.7f));

        // Bolas de fuego
        ParticleSystem fire = MakeSystem("Fire", flash.transform, Look.Fire);
        var main = fire.main;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startColor = explosionColor;
        Burst(fire, 14, 18);
        var shape = fire.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;
        Slowdown(fire, 0.12f);
        var col = fire.colorOverLifetime;
        col.enabled = true;
        col.color = Gradient3(new Color(1f, 0.95f, 0.6f), explosionColor, new Color(0.35f, 0.15f, 0.1f), 1f, 0.9f, 0f);
        var size = fire.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.8f, 1f, 0.2f));

        // Humo que se queda
        ParticleSystem smoke = MakeSystem("Smoke", flash.transform);
        var sm = smoke.main;
        sm.duration = 0.4f;
        sm.startDelay = 0.05f;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        sm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        Color smokeColor = new Color(0.35f, 0.32f, 0.35f, 0.8f);
        sm.startColor = smokeColor;
        sm.gravityModifier = -0.06f;
        Burst(smoke, 10, 12);
        var ss = smoke.shape;
        ss.enabled = true;
        ss.shapeType = ParticleSystemShapeType.Circle;
        ss.radius = 0.5f;
        Slowdown(smoke, 0.08f);
        PopAndShrink(smoke, smokeColor);

        // Chispas
        ParticleSystem sparks = MakeSystem("Sparks", flash.transform, Look.Spark);
        var km = sparks.main;
        km.duration = 0.2f;
        km.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        km.startSpeed = new ParticleSystem.MinMaxCurve(6f, 11f);
        km.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
        km.startColor = new Color(1f, 0.85f, 0.4f);
        Burst(sparks, 14, 18);
        var ks = sparks.shape;
        ks.enabled = true;
        ks.shapeType = ParticleSystemShapeType.Circle;
        ks.radius = 0.2f;
        Slowdown(sparks, 0.1f);
        GrowAndFade(sparks, 1f, 0.3f, new Color(1f, 0.85f, 0.4f));
        Stretch(sparks, 0.05f);
        return flash;
    }

    // ---------- Ayudantes ----------
    enum Look { Smoke, Spark, Flash, Fire, Star }

    ParticleSystem MakeSystem(string objectName, Transform parent, Look look = Look.Smoke)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 64;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = false;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = MaterialFor(look);
        ApplySheet(ps, look);
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortingLayerName = sortingLayerName;
        r.sortingOrder = sortingOrder;
        return ps;
    }

    static void Burst(ParticleSystem ps, int min, int max)
    {
        var emission = ps.emission;
        emission.enabled = true;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)min, (short)max) });
    }

    // Emite en un cono que apunta hacia +X: con la rotacion del efecto, sale en la direccion que se pasa a Play
    static void ConeAlongX(ParticleSystem ps, float angle)
    {
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = angle;
        shape.radius = 0.05f;
        shape.rotation = new Vector3(0f, 90f, 0f);
    }

    static void Slowdown(ParticleSystem ps, float dampen)
    {
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 0f;
        limit.dampen = dampen;
    }

    static void GrowAndFade(ParticleSystem ps, float startSize, float endSize, Color color)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, startSize, 1f, endSize));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Gradient3(color, color, color, 1f, 0.7f, 0f);
    }

    // Estilo cartoon: aparece de golpe un poco mas grande, se asienta y se encoge hasta desaparecer (sin desvanecerse lento)
    static void PopAndShrink(ParticleSystem ps, Color color)
    {
        var curve = new AnimationCurve(
            new Keyframe(0f, 0.4f, 0f, 8f),
            new Keyframe(0.15f, 1.15f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 0f, -1.5f, 0f));
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Gradient3(color, color, color, 1f, 1f, 0.9f);
    }

    static void Spin(ParticleSystem ps, float degreesPerSecond)
    {
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        float r = degreesPerSecond * Mathf.Deg2Rad;
        rot.z = new ParticleSystem.MinMaxCurve(-r, r);
    }

    static void Stretch(ParticleSystem ps, float velocityScale)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = velocityScale;
        r.lengthScale = 1.5f;
    }

    static Gradient Gradient3(Color a, Color b, Color c, float alphaA, float alphaB, float alphaC)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.4f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(alphaA, 0f), new GradientAlphaKey(alphaB, 0.5f), new GradientAlphaKey(alphaC, 1f) });
        return g;
    }

    readonly Dictionary<Look, Material> looks = new Dictionary<Look, Material>();

    // Un material por aspecto: copia del material base con el dibujo de ese efecto
    Material MaterialFor(Look look)
    {
        if (looks.TryGetValue(look, out Material m) && m != null) return m;

        bool glow = look == Look.Spark || look == Look.Flash;
        Material baseMat = glow && glowMaterial != null ? glowMaterial : material;
        if (baseMat == null) return null;

        Texture tex = TextureFor(look);
        m = new Material(baseMat) { name = "Vfx " + look, hideFlags = HideFlags.HideAndDontSave };
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        looks[look] = m;
        return m;
    }

    Texture TextureFor(Look look)
    {
        switch (look)
        {
            case Look.Spark: return sparkTexture != null ? sparkTexture : starTexture != null ? starTexture : (Texture)softDot;
            case Look.Star: return starTexture != null ? starTexture : sparkTexture != null ? sparkTexture : (Texture)softDot;
            case Look.Flash: return flashTexture != null ? flashTexture : (Texture)softDot;
            case Look.Fire: return fireTexture != null ? fireTexture : (smokeTexture != null ? smokeTexture : (Texture)softDot);
            default: return smokeTexture != null ? smokeTexture : (Texture)softDot;
        }
    }

    // Si el dibujo trae varias variaciones en cuadricula, cada particula usa una al azar
    void ApplySheet(ParticleSystem ps, Look look)
    {
        Vector2Int tiles = look == Look.Star ? (starTexture != null ? starSheet : sparkSheet)
                         : look == Look.Spark ? sparkSheet
                         : look == Look.Fire ? (fireTexture != null ? fireSheet : smokeSheet)
                         : look == Look.Smoke ? smokeSheet : Vector2Int.one;
        if (tiles.x * tiles.y <= 1) return;

        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = Mathf.Max(1, tiles.x);
        sheet.numTilesY = Mathf.Max(1, tiles.y);
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        // Cada particula elige un dibujo al azar de la cuadricula y se queda con el (no se anima)
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f);
        sheet.cycleCount = 1;
    }

    void SetupMaterial()
    {
        if (softDot == null) softDot = MakeSoftDot(32);

        if (particleMaterial != null)
        {
            material = particleMaterial;
            string shaderName = material.shader != null ? material.shader.name : "";
            if (shaderName.Contains("Lit") && !shaderName.Contains("Unlit"))
                Debug.LogWarning("VfxManager: el material de particulas usa '" + shaderName + "'. Sin luces 2D se ve NEGRO. " +
                                 "Usa 'Universal Render Pipeline/Particles/Unlit' o 'Sprite-Unlit-Default'.", this);
            return;
        }

        string[] shaders =
        {
            "Universal Render Pipeline/2D/Sprite-Unlit-Default",
            "Universal Render Pipeline/Particles/Unlit",
            "Sprites/Default",
            "Legacy Shaders/Particles/Alpha Blended"
        };
        foreach (var s in shaders)
        {
            Shader shader = Shader.Find(s);
            if (shader == null) continue;
            material = new Material(shader) { name = "Vfx (auto)", hideFlags = HideFlags.HideAndDontSave };
            break;
        }

        if (material == null)
        {
            Debug.LogWarning("VfxManager: no encontre un shader para las particulas. Asigna un material en 'Particle Material'.", this);
            return;
        }

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);   // Particles/Unlit: transparente
    }

    // Circulo blanco con borde suave: la forma basica de todas las particulas
    static Texture2D MakeSoftDot(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "VfxSoftDot",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        float r = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r)) / r;
                float a = 1f - Mathf.SmoothStep(0.55f, 1f, d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }
}
