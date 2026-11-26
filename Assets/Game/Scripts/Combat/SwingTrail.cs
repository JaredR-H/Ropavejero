using UnityEngine;

/// Estela del swing de un arma: una "cinta" que barre toda la hoja (del mango a la punta) mientras el arma se mueve.
/// La crean solos WeaponVisual (jugador) y EnemyWeaponVisual (enemigos); tambien puedes agregarla a mano al objeto
/// del SpriteRenderer del arma para ajustar sus valores en el Inspector.
/// El sprite del arma debe estar dibujado apuntando a la DERECHA (mango a la izquierda, punta a la derecha).
[DisallowMultipleComponent]
public class SwingTrail : MonoBehaviour
{
    [Tooltip("Segundos que dura la estela detras del arma. Mas = estela mas larga.")]
    [SerializeField, Min(0.01f)] float trailTime = 0.14f;
    [Tooltip("Que parte de la hoja cubre la estela (1 = todo el largo del sprite, 0.6 = solo la parte de la punta).")]
    [SerializeField, Range(0.1f, 1f)] float bladeCoverage = 0.75f;
    [Tooltip("Color de la estela: izquierda = pegado al arma, derecha = la cola que desaparece.")]
    [SerializeField] Gradient color = DefaultGradient();
    [Tooltip("Ancho a lo largo de la estela (1 = toda la hoja). Se afina hacia la cola.")]
    [SerializeField] AnimationCurve width = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.35f));
    [Tooltip("Orden de dibujo respecto al arma (-1 = justo detras del arma).")]
    [SerializeField] int sortingOffset = -1;
    [Tooltip("Material SIN luz. Vacio = Sprite-Unlit-Default (se busca solo).")]
    [SerializeField] Material material;

    SpriteRenderer weapon;
    TrailRenderer trail;
    Transform holder;
    static Material autoMaterial;

    static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.35f, 0.5f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    /// Busca (o crea) la estela del SpriteRenderer de un arma.
    public static SwingTrail For(SpriteRenderer weaponRenderer)
    {
        if (weaponRenderer == null) return null;
        var t = weaponRenderer.GetComponent<SwingTrail>();
        if (t == null) t = weaponRenderer.gameObject.AddComponent<SwingTrail>();
        return t;
    }

    void Awake()
    {
        weapon = GetComponent<SpriteRenderer>();
        holder = new GameObject("SwingTrail").transform;
        holder.SetParent(transform, false);
        trail = holder.gameObject.AddComponent<TrailRenderer>();
        trail.emitting = false;
        trail.time = trailTime;
        trail.minVertexDistance = 0.02f;
        trail.numCapVertices = 2;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.colorGradient = color;
        trail.widthCurve = width;
        var mat = material != null ? material : AutoMaterial();
        if (mat != null) trail.sharedMaterial = mat;
    }

    static Material AutoMaterial()
    {
        if (autoMaterial != null) return autoMaterial;
        var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) return null;
        autoMaterial = new Material(sh) { name = "SwingTrail (auto)" };
        return autoMaterial;
    }

    /// Empieza a dibujar la estela (al empezar la parte rapida del golpe).
    public void Begin()
    {
        if (trail == null) return;
        Place();
        trail.Clear();
        trail.emitting = true;
    }

    /// Deja de dibujar; lo que ya se dibujo se desvanece solo.
    public void End()
    {
        if (trail != null) trail.emitting = false;
    }

    /// Corta y borra la estela de inmediato (arma guardada, enemigo derrotado...).
    public void Clear()
    {
        if (trail == null) return;
        trail.emitting = false;
        trail.Clear();
    }

    void LateUpdate() => Place();

    // La estela va en el centro de la parte de la hoja que cubre, y su ancho es ese largo: asi barre la hoja entera
    void Place()
    {
        if (trail == null || weapon == null || weapon.sprite == null) return;

        Bounds b = weapon.sprite.bounds;                 // en unidades locales del sprite
        float length = b.size.x * bladeCoverage;
        float centerX = b.max.x - length * 0.5f;         // desde la punta hacia el mango
        if (weapon.flipX) centerX = -centerX;
        holder.localPosition = new Vector3(centerX, b.center.y, 0f);

        Vector3 s = transform.lossyScale;
        trail.widthMultiplier = length * Mathf.Abs(s.x);

        trail.sortingLayerID = weapon.sortingLayerID;
        trail.sortingOrder = weapon.sortingOrder + sortingOffset;
    }

    void OnDisable()
    {
        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }
    }
}
