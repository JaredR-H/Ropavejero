using System.Collections;
using UnityEngine;

public class GameFeel : MonoBehaviour
{
    public static GameFeel Instance { get; private set; }

    [SerializeField] Camera cam;

    [Header("Golpe a un enemigo (sutil)")]
    [SerializeField] float enemyHitTrauma = 0.22f;
    [SerializeField] float enemyHitStop = 0.045f;

    [Header("Explosion")]
    [SerializeField] float explosionTrauma = 0.6f;
    [SerializeField] float explosionHitStop = 0.06f;

    [Header("Golpe al jugador (muerte)")]
    [SerializeField] float deathHitStop = 0.1f;
    [SerializeField] float deathTrauma = 0.9f;
    [Tooltip("Cuanto se acerca la camara al jugador (1 = nada, 1.35 = acercamiento pequeno).")]
    [SerializeField] float deathZoom = 1.35f;
    [Tooltip("Que tan rapido se acerca y se aleja el zoom.")]
    [SerializeField] float zoomSpeed = 14f;
    [Range(0.05f, 1f)]
    [Tooltip("Velocidad del juego en camara lenta (0.2 = al 20%). Tambien afecta al tiempo de la sala.")]
    [SerializeField] float deathSlowScale = 0.2f;
    [Tooltip("Segundos REALES que dura la camara lenta.")]
    [SerializeField] float deathSlowDuration = 0.55f;

    [Header("Shake")]
    [SerializeField] float maxShakeOffset = 0.5f;
    [SerializeField] float shakeFrequency = 22f;
    [Tooltip("Cuanto baja el shake por segundo.")]
    [SerializeField] float traumaDecay = 2.2f;
    [Tooltip("Empujon de la camara en la direccion del golpe.")]
    [SerializeField] float kickStrength = 0.35f;
    [SerializeField] float kickDecay = 12f;

    Vector3 camBase;
    float baseSize;
    float baseFixedDelta;

    float trauma;
    float seedX, seedY;
    Vector2 kick;

    float zoomCurrent = 1f;
    float zoomTarget = 1f;
    Transform zoomFocus;
    Vector3 zoomFocusPos;

    float hitStopUntil;
    float slowUntil;
    float slowScale = 1f;
    bool frozen;
    bool controllingTime;

    public Vector3 CameraBase
    {
        get => camBase;
        set => camBase = value;
    }

    void Awake()
    {
        Instance = this;
        if (cam == null) cam = Camera.main;
        baseFixedDelta = Time.fixedDeltaTime;
        if (cam != null)
        {
            camBase = cam.transform.position;
            baseSize = cam.orthographicSize;
        }
        seedX = Random.value * 100f;
        seedY = Random.value * 100f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ReleaseTime();
    }

    // ---------- Efectos sueltos ----------
    public void EnemyHit(Vector2 direction)
    {
        AddShake(enemyHitTrauma, direction);
        HitStop(enemyHitStop);
    }

    /// Explosion con los valores por defecto de este componente.
    public void Explosion() => Explosion(explosionTrauma, explosionHitStop);

    /// Explosion con intensidad de vibracion y hitstop propios (cada enemigo elige los suyos).
    public void Explosion(float trauma, float hitStopSeconds)
    {
        AddShake(trauma);
        HitStop(hitStopSeconds);
    }

    public void HitStop(float seconds)
    {
        if (seconds <= 0f) return;
        hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds);
    }

    public void SlowMotion(float scale, float seconds)
    {
        slowScale = Mathf.Clamp(scale, 0.05f, 1f);
        slowUntil = Mathf.Max(slowUntil, Time.unscaledTime + seconds);
    }

    /// Shake no uniforme: cada llamada cambia la semilla y la intensidad, y suma un empujon en la direccion del golpe.
    public void AddShake(float amount, Vector2 direction = default)
    {
        trauma = Mathf.Clamp01(trauma + amount * Random.Range(0.8f, 1.2f));
        seedX = Random.value * 100f;
        seedY = Random.value * 100f;
        if (direction.sqrMagnitude > 0.001f)
            kick += direction.normalized * (amount * kickStrength * Random.Range(0.7f, 1.3f));
    }

    // ---------- Muerte del jugador ----------
    public IEnumerator DeathSequence(Transform target, Vector2 hitDirection)
    {
        zoomFocus = target;
        zoomTarget = deathZoom;
        AddShake(deathTrauma, hitDirection);
        HitStop(deathHitStop);
        yield return new WaitForSecondsRealtime(deathHitStop);

        SlowMotion(deathSlowScale, deathSlowDuration);
        yield return new WaitForSecondsRealtime(deathSlowDuration);

        frozen = true;     // el tiempo se queda quieto hasta que el jugador reaparezca
    }

    /// Fin de la muerte: tiempo normal y el zoom vuelve solo.
    public void Release()
    {
        frozen = false;
        hitStopUntil = 0f;
        slowUntil = 0f;
        zoomTarget = 1f;
        zoomFocus = null;
        ReleaseTime();
    }

    // ---------- Tiempo ----------
    void Update()
    {
        var gm = GameManager.Instance;
        bool allowed = gm == null || gm.State == GameState.Playing || gm.State == GameState.Dead;
        if (!allowed)
        {
            // Transiciones, game over y victoria mandan sobre el tiempo: se cancelan los efectos.
            hitStopUntil = 0f;
            slowUntil = 0f;
            frozen = false;
            if (controllingTime) ReleaseTime();
            return;
        }

        float now = Time.unscaledTime;
        if (frozen || now < hitStopUntil)
        {
            ApplyScale(0f);
            controllingTime = true;
        }
        else if (now < slowUntil)
        {
            ApplyScale(slowScale);
            controllingTime = true;
        }
        else if (controllingTime)
        {
            ReleaseTime();
        }
    }

    void ApplyScale(float scale)
    {
        Time.timeScale = scale;
        Time.fixedDeltaTime = baseFixedDelta * Mathf.Max(scale, 0.1f);   // la fisica sigue fluida en camara lenta
    }

    void ReleaseTime()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDelta;
        controllingTime = false;
    }

    // ---------- Camara ----------
    void LateUpdate()
    {
        if (cam == null) return;
        float dt = Time.unscaledDeltaTime;

        // Zoom hacia el jugador (la vista queda siempre dentro de la sala)
        if (zoomFocus != null) zoomFocusPos = zoomFocus.position;
        zoomCurrent = Mathf.Lerp(zoomCurrent, zoomTarget, 1f - Mathf.Exp(-zoomSpeed * dt));
        float z = Mathf.Max(1f, zoomCurrent);

        Vector3 pos = camBase;
        if (z > 1.001f)
        {
            Vector3 shift = (zoomFocusPos - camBase) * (1f - 1f / z);
            pos.x += shift.x;
            pos.y += shift.y;
        }
        cam.orthographicSize = baseSize / z;

        // Shake
        trauma = Mathf.MoveTowards(trauma, 0f, traumaDecay * dt);
        kick = Vector2.Lerp(kick, Vector2.zero, 1f - Mathf.Exp(-kickDecay * dt));
        Vector2 offset = kick;
        if (trauma > 0f)
        {
            float strength = trauma * trauma * maxShakeOffset;
            float t = Time.unscaledTime * shakeFrequency;
            offset.x += (Mathf.PerlinNoise(seedX, t) * 2f - 1f) * strength;
            offset.y += (Mathf.PerlinNoise(seedY, t) * 2f - 1f) * strength;
        }

        cam.transform.position = new Vector3(pos.x + offset.x, pos.y + offset.y, camBase.z);
    }
}
