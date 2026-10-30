using System.Collections.Generic;
using UnityEngine;

/// Squash & stretch y balanceo por curvas. Se pone en el objeto que se quiere animar (normalmente el hijo "Visual"
/// del personaje, el sprite del arma, un recolectable o un elemento de UI).
///  - Reposo y caminar: continuos; SetMove(0..1) mezcla uno con otro.
///  - Animaciones de un disparo: Play("Dash", direccion), Play("Charge", ...), etc. Se pueden superponer.
/// Convive con otros scripts que muevan el mismo Transform (por ejemplo el salto del jugador): suma sus efectos
/// encima y no pisa lo que ellos hagan. Para que el estiramiento crezca desde los pies, pon el pivote del sprite abajo.
public class JuiceAnimator : MonoBehaviour
{
    [Tooltip("Perfil con las curvas. Si esta vacio se usa uno por defecto; crea uno propio para editarlo (Create > Ropavejero > Juice Profile).")]
    [SerializeField] JuiceProfile profile;
    [Tooltip("Multiplicador de TODO el efecto en este objeto (0 = apagado, 1 = normal).")]
    [SerializeField, Min(0f)] float intensity = 1f;
    [Tooltip("Marcalo para UI o cosas que deben animarse con el juego en pausa.")]
    [SerializeField] bool useUnscaledTime = false;

    struct ActiveShot
    {
        public ShotMotion motion;
        public float time;
        public float duration;
        public float intensity;
        public bool horizontal;
    }

    readonly List<ActiveShot> shots = new List<ActiveShot>();
    JuiceProfile active;
    bool createdProfile;
    RectTransform rect;

    float idlePhase, movePhase;
    float moveBlend, moveTarget;

    // Lo ultimo que aplicamos, para no pisar a otros scripts
    bool applied;
    Vector3 lastPos, lastOffset, lastScale, lastFactor;
    Quaternion lastRot, lastRotOffset;

    void Awake()
    {
        rect = transform as RectTransform;
        SetProfile(profile);
        // Fase al azar: dos enemigos iguales no se balancean sincronizados.
        idlePhase = Random.value;
        movePhase = Random.value;
    }

    void OnDestroy()
    {
        if (createdProfile && active != null) Destroy(active);
    }

    void OnDisable()
    {
        Restore();
        shots.Clear();
    }

    public void SetProfile(JuiceProfile newProfile)
    {
        if (createdProfile && active != null) Destroy(active);
        createdProfile = false;

        if (newProfile != null)
        {
            active = newProfile;
        }
        else
        {
            active = ScriptableObject.CreateInstance<JuiceProfile>();
            createdProfile = true;
        }
    }

    // ---------- Control ----------
    /// 0 = reposo, 1 = caminando. Se mezcla suavemente.
    public void SetMove(float amount) => moveTarget = Mathf.Clamp01(amount);

    /// Reproduce una animacion de un disparo por su nombre. direction solo importa si su eje es FromDirection.
    /// duration > 0 reemplaza la duracion del perfil. intensity multiplica el efecto de esta reproduccion.
    public void Play(string shotName, Vector2 direction = default, float duration = 0f, float intensity = 1f)
    {
        if (active == null) return;
        ShotMotion motion = active.FindShot(shotName);
        if (motion == null) return;

        bool horizontal;
        switch (motion.axis)
        {
            case JuiceAxis.Horizontal: horizontal = true; break;
            case JuiceAxis.FromDirection:
                horizontal = direction.sqrMagnitude > 0.0001f && Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);
                break;
            default: horizontal = false; break;
        }

        // Si ya hay una igual, se reinicia.
        Stop(shotName);
        shots.Add(new ActiveShot
        {
            motion = motion,
            time = 0f,
            duration = duration > 0f ? duration : motion.duration,
            intensity = intensity,
            horizontal = horizontal
        });
    }

    /// Para llamarlo desde un UnityEvent (por ejemplo el OnClick de un boton).
    public void PlayByName(string shotName) => Play(shotName);

    public void Stop(string shotName)
    {
        for (int i = shots.Count - 1; i >= 0; i--)
            if (string.Equals(shots[i].motion.name, shotName, System.StringComparison.OrdinalIgnoreCase))
                shots.RemoveAt(i);
    }

    public void StopAll() => shots.Clear();

    // ---------- Calculo ----------
    void LateUpdate()
    {
        if (active == null) return;
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        moveBlend = Mathf.MoveTowards(moveBlend, moveTarget, active.blendSpeed * dt);
        idlePhase = Mathf.Repeat(idlePhase + dt / Mathf.Max(0.05f, active.idle.period), 1f);
        movePhase = Mathf.Repeat(movePhase + dt / Mathf.Max(0.05f, active.move.period), 1f);

        float fx = 1f, fy = 1f, rot = 0f, y = 0f;

        AddLoop(active.idle, idlePhase, 1f - moveBlend, ref fx, ref fy, ref rot, ref y);
        AddLoop(active.move, movePhase, moveBlend, ref fx, ref fy, ref rot, ref y);

        for (int i = shots.Count - 1; i >= 0; i--)
        {
            ActiveShot s = shots[i];
            s.time += dt;
            if (s.time >= s.duration)
            {
                shots.RemoveAt(i);
                continue;
            }
            shots[i] = s;

            float t = s.time / s.duration;
            ShotMotion m = s.motion;
            float k = s.intensity * intensity;
            AddStretch(m.stretch.Evaluate(t) * m.stretchAmount * k, s.horizontal, m.preserveVolume, ref fx, ref fy);
            rot += m.rotation.Evaluate(t) * m.rotationAmount * k;
            y += m.yOffset.Evaluate(t) * m.yOffsetAmount * k;
        }

        Apply(Mathf.Clamp(fx, 0.2f, 3f), Mathf.Clamp(fy, 0.2f, 3f), rot, y);
    }

    void AddLoop(LoopMotion m, float phase, float weight, ref float fx, ref float fy, ref float rot, ref float y)
    {
        if (weight <= 0.0001f) return;
        float k = weight * intensity;
        AddStretch(m.stretch.Evaluate(phase) * m.stretchAmount * k, false, true, ref fx, ref fy);
        rot += m.rotation.Evaluate(phase) * m.rotationAmount * k;
        y += m.yOffset.Evaluate(phase) * m.yOffsetAmount * k;
    }

    static void AddStretch(float s, bool horizontal, bool preserveVolume, ref float fx, ref float fy)
    {
        s = Mathf.Max(s, -0.85f);
        float main = 1f + s;
        float other = preserveVolume ? 1f / main : 1f;
        if (horizontal) { fx *= main; fy *= other; }
        else { fy *= main; fx *= other; }
    }

    // ---------- Aplicar sin pisar a otros scripts ----------
    // Si el Transform sigue exactamente como lo dejamos, le quitamos nuestro efecto anterior para obtener su valor base.
    // Si otro script lo cambio (salto, animator...), ese valor nuevo ya es la base.
    void Apply(float fx, float fy, float rotDeg, float yOffset)
    {
        Transform t = transform;

        Vector3 pos = GetPos();
        Vector3 basePos = applied && pos == lastPos ? lastPos - lastOffset : pos;
        Vector3 newOffset = new Vector3(0f, yOffset, 0f);
        SetPos(basePos + newOffset);

        Vector3 scale = t.localScale;
        Vector3 baseScale = applied && scale == lastScale ? Divide(lastScale, lastFactor) : scale;
        Vector3 newFactor = new Vector3(fx, fy, 1f);
        t.localScale = Vector3.Scale(baseScale, newFactor);

        Quaternion rot = t.localRotation;
        Quaternion baseRot = applied && rot == lastRot ? lastRot * Quaternion.Inverse(lastRotOffset) : rot;
        Quaternion newRotOffset = Quaternion.Euler(0f, 0f, rotDeg);
        t.localRotation = baseRot * newRotOffset;

        lastPos = GetPos();
        lastOffset = newOffset;
        lastScale = t.localScale;
        lastFactor = newFactor;
        lastRot = t.localRotation;
        lastRotOffset = newRotOffset;
        applied = true;
    }

    void Restore()
    {
        if (!applied) return;
        Transform t = transform;

        if (GetPos() == lastPos) SetPos(lastPos - lastOffset);
        if (t.localScale == lastScale) t.localScale = Divide(lastScale, lastFactor);
        if (t.localRotation == lastRot) t.localRotation = lastRot * Quaternion.Inverse(lastRotOffset);

        applied = false;
    }

    Vector3 GetPos() => rect != null ? rect.anchoredPosition3D : transform.localPosition;

    void SetPos(Vector3 p)
    {
        if (rect != null) rect.anchoredPosition3D = p;
        else transform.localPosition = p;
    }

    static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
}
