using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpikeTrap : MonoBehaviour, IResettable
{
    enum State { Safe = 0, Warning = 1, Active = 2 }

    [Header("Tiempos (segundos)")]
    [Tooltip("Espera inicial al entrar a la sala o reaparecer, antes de empezar el ciclo")]
    [SerializeField, Min(0f)] float startDelay = 0f;
    [Tooltip("Tiempo con las puas escondidas")]
    [SerializeField, Min(0f)] float inactiveTime = 2f;
    [Tooltip("Tiempo de aviso (sprite 2). 0 = sin aviso")]
    [SerializeField, Min(0f)] float warningTime = 0.4f;
    [Tooltip("Tiempo con las puas levantadas (sprite 3). Aqui es cuando matan")]
    [SerializeField, Min(0.01f)] float activeTime = 1f;

    [Header("Hitbox")]
    [Tooltip("Collider2D que mata. Si esta vacio usa el del mismo objeto. Se fuerza a Is Trigger")]
    [SerializeField] Collider2D hitbox;

    [Header("Animacion (opcional)")]
    [Tooltip("Si tiene Animator con el parametro Int de abajo, la animacion manda y los sprites se ignoran")]
    [SerializeField] Animator animator;
    [SerializeField] string stateParameter = "State";

    [Header("Sprites por estado (solo si NO hay Animator)")]
    [SerializeField] SpriteRenderer sr;
    [SerializeField] Sprite safeSprite;      // sprite 1: escondidas
    [SerializeField] Sprite warningSprite;   // sprite 2: a punto de salir
    [SerializeField] Sprite activeSprite;    // sprite 3: levantadas

    State state = State.Safe;
    float startTime;
    bool initialized;
    bool useAnimator;
    int stateId;
    Sprite fallbackSprite;

    void Awake() => Init();

    void Init()
    {
        if (initialized) return;
        initialized = true;

        if (hitbox == null) hitbox = GetComponent<Collider2D>();
        hitbox.isTrigger = true;
        hitbox.enabled = false;

        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null) fallbackSprite = sr.sprite;

        if (animator == null) animator = GetComponent<Animator>();
        stateId = Animator.StringToHash(stateParameter);
        useAnimator = animator != null
                      && animator.runtimeAnimatorController != null
                      && HasIntParameter(animator, stateParameter);

        if (animator != null && !useAnimator)
            Debug.LogWarning($"SpikeTrap '{name}': el Animator no tiene el parametro Int '{stateParameter}'. Se usaran los sprites por estado.", this);
    }

    void OnEnable() => ResetState();

    public void ResetState()
    {
        Init();
        startTime = Time.time;
        SetState(State.Safe);
    }

    void Update()
    {
        float t = Time.time - startTime - startDelay;

        State wanted;
        if (t < 0f)
        {
            wanted = State.Safe;
        }
        else
        {
            float cycle = inactiveTime + warningTime + activeTime;
            float phase = t % cycle;
            if (phase < inactiveTime) wanted = State.Safe;
            else if (phase < inactiveTime + warningTime) wanted = State.Warning;
            else wanted = State.Active;
        }

        if (wanted != state) SetState(wanted);
    }

    void SetState(State s)
    {
        state = s;
        hitbox.enabled = (s == State.Active);   // el collider solo existe con los spikes levantados

        if (useAnimator)
        {
            if (isActiveAndEnabled) animator.SetInteger(stateId, (int)s);
            return;
        }

        if (sr == null) return;
        Sprite wanted = s == State.Active ? activeSprite : (s == State.Warning ? warningSprite : safeSprite);
        sr.sprite = wanted != null ? wanted : fallbackSprite;
    }

    static bool HasIntParameter(Animator a, string paramName)
    {
        foreach (var p in a.parameters)
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Int) return true;
        return false;
    }

    // Stay (no Enter) para matar tambien si las puas suben justo debajo del jugador.
    void OnTriggerStay2D(Collider2D other)
    {
        if (state == State.Active) HazardUtil.TryKillPlayer(other);
    }
}