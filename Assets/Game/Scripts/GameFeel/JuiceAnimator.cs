using System.Collections.Generic;
using UnityEngine;

public class JuiceAnimator : MonoBehaviour
{
    [Tooltip("Perfil con las curvas. Si esta vacio se usa uno por defecto; crea uno propio para editarlo (Create > Ropavejero > Juice Profile).")]
    [SerializeField] JuiceProfile profile;
    [Tooltip("Multiplicador de TODO el efecto en este objeto (0 = apagado, 1 = normal).")]
    [SerializeField, Min(0f)] float intensity = 1f;
    [Tooltip("Marcalo para UI o cosas que deben animarse con el juego en pausa.")]
    [SerializeField] bool useUnscaledTime = false;
    [Tooltip("Desmarcalo si solo quieres animaciones puntuales (botones, textos): sin el balanceo continuo de reposo y caminar.")]
    [SerializeField] bool playLoops = true;

    struct ActiveShot
    {
        public ShotMotion motion;
        public float time;
        public float duration;
        public float intensity;
        public bool horizontal;
        public float rotSign;
    }

    readonly List<ActiveShot> shots = new List<ActiveShot>();
    JuiceProfile active;
    bool createdProfile;
    RectTransform rect;

    float idlePhase, movePhase;
    float moveBlend, moveTarget;
    float scaleMul = 1f, scaleTarget = 1f, scaleSpeed = 10f;

    // Efecto manual (por ejemplo la carga de un ataque): lo controla otro script cada frame
    bool overlayActive;
    float overlayRot, overlayStretch, overlayMaxCross = 3f;
    Vector2 overlayPos;
    bool overlayScaleActive;
    float overlayScaleX = 1f, overlayScaleY = 1f;

    static JuiceProfile fallbackProfile;

    /// Perfil por defecto compartido: aporta las animaciones que le falten a un perfil antiguo.
    static JuiceProfile Fallback
    {
        get
        {
            if (fallbackProfile == null)
            {
                fallbackProfile = ScriptableObject.CreateInstance<JuiceProfile>();
                fallbackProfile.hideFlags = HideFlags.HideAndDontSave;
            }
            return fallbackProfile;
        }
    }

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

    /// Configuracion rapida por codigo (UI): perfil (null = no cambia), tiempo real y si hay balanceo continuo.
    public void Configure(JuiceProfile newProfile, bool unscaled, bool loops)
    {
        useUnscaledTime = unscaled;
        playLoops = loops;
        if (newProfile != null) SetProfile(newProfile);
    }

    // ---------- Control ----------
    /// 0 = reposo, 1 = caminando. Se mezcla suavemente.
    public void SetMove(float amount) => moveTarget = Mathf.Clamp01(amount);

    /// Tamano permanente (1 = normal) al que se acerca con suavidad: crecer al pasar el mouse, aviso de poco tiempo...
    public void SetScaleMultiplier(float target, float speed = 10f)
    {
        scaleTarget = Mathf.Max(0.01f, target);
        scaleSpeed = Mathf.Max(0.1f, speed);
    }

    /// Efecto manual encima de todo lo demas: rotacion (grados), estiramiento (negativo = aplasta) y desplazamiento.
    /// Hay que llamarlo cada frame mientras dure; se quita con ClearOverlay.
    public void SetOverlay(float rotationDegrees, float stretch, Vector2 positionOffset, float maxCrossScale = 3f)
    {
        overlayActive = true;
        overlayRot = rotationDegrees;
        overlayStretch = stretch;
        overlayPos = positionOffset;
        overlayMaxCross = maxCrossScale;
    }

    /// Escala manual en ancho y alto por separado (1 = normal; 0 = desaparece en ese eje). Se quita con ClearOverlay.
    public void SetOverlayScale(float scaleX, float scaleY)
    {
        overlayScaleActive = true;
        overlayScaleX = scaleX;
        overlayScaleY = scaleY;
    }

    public void ClearOverlay()
    {
        overlayActive = false;
        overlayScaleActive = false;
    }

    /// Fija el tamano al instante, sin transicion.
    public void SnapScale(float value)
    {
        scaleMul = scaleTarget = Mathf.Max(0.01f, value);
    }

    /// Reproduce una animacion de un disparo por su nombre. direction solo importa si su eje es FromDirection.
    /// duration > 0 reemplaza la duracion del perfil. intensity multiplica el efecto de esta reproduccion.
    public void Play(string shotName, Vector2 direction = default, float duration = 0f, float intensity = 1f)
    {
        if (active == null) return;
        ShotMotion motion = active.FindShot(shotName) ?? Fallback.FindShot(shotName);
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
            horizontal = horizontal,
            rotSign = motion.mirrorRotationByDirection && direction.x < 0f ? -1f : 1f
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

        float fx = 1f, fy = 1f, rot = 0f, x = 0f, y = 0f;

        if (playLoops)
        {
            AddLoop(active.idle, idlePhase, 1f - moveBlend, ref fx, ref fy, ref rot, ref y);
            AddLoop(active.move, movePhase, moveBlend, ref fx, ref fy, ref rot, ref y);
        }

        // Tamano permanente (hover, urgencia...)
        scaleMul = Mathf.Lerp(scaleMul, scaleTarget, 1f - Mathf.Exp(-scaleSpeed * dt));
        fx *= scaleMul;
        fy *= scaleMul;

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
            AddStretch(m.stretch.Evaluate(t) * m.stretchAmount * k, s.horizontal, m.preserveVolume, m.maxCrossScale, ref fx, ref fy);
            rot += m.rotation.Evaluate(t) * m.rotationAmount * k * s.rotSign;
            y += m.yOffset.Evaluate(t) * m.yOffsetAmount * k;
        }

        if (overlayActive)
        {
            AddStretch(overlayStretch, false, true, overlayMaxCross, ref fx, ref fy);
            rot += overlayRot;
            x += overlayPos.x;
            y += overlayPos.y;
        }

        if (overlayScaleActive)
        {
            fx *= overlayScaleX;
            fy *= overlayScaleY;
        }

        Apply(Mathf.Clamp(fx, 0.01f, 3f), Mathf.Clamp(fy, 0.01f, 3f), rot, x, y);
    }

    void AddLoop(LoopMotion m, float phase, float weight, ref float fx, ref float fy, ref float rot, ref float y)
    {
        if (weight <= 0.0001f) return;
        float k = weight * intensity;
        AddStretch(m.stretch.Evaluate(phase) * m.stretchAmount * k, false, true, 3f, ref fx, ref fy);
        rot += m.rotation.Evaluate(phase) * m.rotationAmount * k;
        y += m.yOffset.Evaluate(phase) * m.yOffsetAmount * k;
    }

    // maxCross limita cuanto se ensancha el otro eje al conservar el volumen (asi se puede aplastar casi hasta una linea
    // sin que se vuelva enorme a lo ancho).
    static void AddStretch(float s, bool horizontal, bool preserveVolume, float maxCross, ref float fx, ref float fy)
    {
        s = Mathf.Max(s, -0.99f);
        float main = 1f + s;
        float other = preserveVolume ? Mathf.Min(1f / main, Mathf.Max(1f, maxCross)) : 1f;
        if (horizontal) { fx *= main; fy *= other; }
        else { fy *= main; fx *= other; }
    }

    // ---------- Aplicar sin pisar a otros scripts ----------
    // Si el Transform sigue exactamente como lo dejamos, le quitamos nuestro efecto anterior para obtener su valor base.
    // Si otro script lo cambio (salto, animator...), ese valor nuevo ya es la base.
    void Apply(float fx, float fy, float rotDeg, float xOffset, float yOffset)
    {
        Transform t = transform;

        Vector3 pos = GetPos();
        Vector3 basePos = applied && pos == lastPos ? lastPos - lastOffset : pos;
        Vector3 newOffset = new Vector3(xOffset, yOffset, 0f);
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
