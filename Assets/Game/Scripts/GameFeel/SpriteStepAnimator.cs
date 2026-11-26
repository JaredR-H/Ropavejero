using UnityEngine;

/// "Animacion" de caminar con solo 2 sprites (quieto y dando un paso), sin Animator.
/// Mientras se mueve alterna quieto -> paso -> quieto -> paso cada cierta distancia recorrida, y en cada sprite de paso
/// hace un saltito con squash & stretch (el disparo "Step" del JuiceAnimator). Asi el ritmo sigue a la velocidad:
/// si camina lento, pasos lentos; si va rapido, pasos rapidos.
/// Va en el objeto RAIZ (el que tiene el Rigidbody2D). Sirve para el jugador y para los enemigos.
/// El volteo (flipX) lo siguen haciendo PlayerController / Enemy: dibuja los sprites de lado mirando a la DERECHA.
[RequireComponent(typeof(Rigidbody2D))]
public class SpriteStepAnimator : MonoBehaviour
{
    [System.Serializable]
    public class StepSprites
    {
        [Tooltip("Sprite quieto (tambien es el 'pie apoyado' entre pasos).")]
        public Sprite idle;
        [Tooltip("Sprite dando un paso. Vacio = solo hace el saltito con el sprite quieto.")]
        public Sprite step;
        [Tooltip("Opcional, solo jugador: sprite durante el dash. Vacio = usa el de paso.")]
        public Sprite dash;

        public bool HasAny => idle != null || step != null;
    }

    [Header("Referencias (vacio = se buscan solas)")]
    [Tooltip("El SpriteRenderer del personaje (NO el de la sombra). Vacio = el que esta junto al JuiceAnimator, o el primero de los hijos.")]
    [SerializeField] SpriteRenderer target;
    [Tooltip("JuiceAnimator del Visual. Hace el saltito de cada paso. Vacio = se busca en los hijos.")]
    [SerializeField] JuiceAnimator juice;

    [Header("Sprites por direccion")]
    [Tooltip("Mirando a la camara (abajo).")]
    [SerializeField] StepSprites front = new StepSprites();
    [Tooltip("De espaldas (arriba). Vacio = usa los de frente.")]
    [SerializeField] StepSprites back = new StepSprites();
    [Tooltip("De lado, dibujado mirando a la DERECHA. Los enemigos que solo tienen un dibujo usan solo este.")]
    [SerializeField] StepSprites side = new StepSprites();

    [Header("Ritmo de los pasos")]
    [Tooltip("Unidades recorridas entre un cambio de sprite y el siguiente. Mayor = pasos mas espaciados ('Ta   Ta   Ta').")]
    [SerializeField, Min(0.05f)] float stepDistance = 0.45f;
    [Tooltip("Por debajo de esta velocidad se considera quieto.")]
    [SerializeField, Min(0f)] float minSpeed = 0.3f;

    [Header("Saltito en cada paso")]
    [SerializeField] bool hopOnStep = true;
    [Tooltip("Fuerza del saltito y del squash & stretch del paso (1 = normal).")]
    [SerializeField, Min(0f)] float hopIntensity = 1f;
    [Tooltip("Duracion del saltito en segundos (0 = la del perfil, 0.26 por defecto).")]
    [SerializeField, Min(0f)] float hopDuration = 0f;
    [Tooltip("Cuanto se conserva del squash & stretch CONTINUO de caminar del JuiceAnimator (el 'tatata'). " +
             "0 = solo el de cada paso. El balanceo (rotacion) se mantiene.")]
    [SerializeField, Range(0f, 1f)] float keepLoopSquash = 0f;

    [Header("Sonido de pasos")]
    [Tooltip("Sonido en cada paso (Footstep para el jugador, EnemyFootstep para enemigos, BossStep para el jefe, None = mudo).")]
    [SerializeField] SfxId stepSound = SfxId.Footstep;
    [SerializeField, Range(0f, 1f)] float stepVolume = 0.6f;

    [Header("Respetar otros scripts")]
    [Tooltip("Si otro script pone un sprite que no es de esta lista (sprite de ataque, aturdido...), no se le pisa hasta que lo devuelva.")]
    [SerializeField] bool respectOtherSprites = true;

    Rigidbody2D rb;
    PlayerController player;
    Vector2 lastPos;
    float traveled;
    bool onStep;
    Sprite lastApplied;
    StepSprites lastSet;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        player = GetComponent<PlayerController>();
        if (juice == null) juice = GetComponentInChildren<JuiceAnimator>(true);
        if (target == null && juice != null) target = juice.GetComponent<SpriteRenderer>();
        if (target == null) target = GetComponentInChildren<SpriteRenderer>(true);
        if (target == null) Debug.LogWarning("SpriteStepAnimator: no encontre el SpriteRenderer del personaje.", this);
        // Si nadie cambio el sonido, los enemigos y el jefe usan el suyo
        if (stepSound == SfxId.Footstep && player == null)
            stepSound = GetComponent<BossController>() != null ? SfxId.BossStep : SfxId.EnemyFootstep;
    }

    void OnEnable()
    {
        if (rb != null) lastPos = rb.position;
        traveled = stepDistance * 0.6f;     // el primer paso sale pronto al empezar a caminar
        onStep = false;
        if (juice != null) juice.SetMoveLoopScale(keepLoopSquash, keepLoopSquash, 1f);
    }

    void OnDisable()
    {
        if (juice != null) juice.SetMoveLoopScale(1f, 1f, 1f);
    }

    void LateUpdate()
    {
        if (target == null || rb == null) return;

        Vector2 pos = rb.position;
        float moved = (pos - lastPos).magnitude;
        lastPos = pos;
        if (moved > 2f) moved = 0f;         // teletransportes (reaparecer, cambiar de sala) no cuentan

        StepSprites set = CurrentSet(rb.linearVelocity);
        bool dashing = player != null && player.IsDashing;
        bool airborne = player != null && player.IsAirborne;
        bool moving = rb.linearVelocity.magnitude > minSpeed && Time.deltaTime > 0f;

        if (dashing)
        {
            Show(set.dash != null ? set.dash : (set.step != null ? set.step : set.idle));
            onStep = false;
            traveled = stepDistance * 0.6f;
            return;
        }

        if (!moving || airborne)
        {
            onStep = false;
            traveled = stepDistance * 0.6f;
            Show(set.idle != null ? set.idle : set.step);
            return;
        }

        traveled += moved;
        if (traveled >= stepDistance)
        {
            traveled = 0f;
            onStep = !onStep;
            if (onStep && hopOnStep && juice != null) juice.Play("Step", rb.linearVelocity, hopDuration, hopIntensity);
            if (onStep) Sfx.Play(stepSound, rb.position, stepVolume);
        }

        Sprite wanted = onStep && set.step != null ? set.step : (set.idle != null ? set.idle : set.step);
        Show(wanted);
    }

    StepSprites CurrentSet(Vector2 velocity)
    {
        bool up, vertical;
        if (player != null)
        {
            vertical = player.Facing == FacingDirection.Up || player.Facing == FacingDirection.Down;
            up = player.Facing == FacingDirection.Up;
        }
        else if (velocity.sqrMagnitude > 0.01f)
        {
            vertical = Mathf.Abs(velocity.y) > Mathf.Abs(velocity.x);
            up = velocity.y > 0f;
        }
        else
        {
            return lastSet ?? Fallback(side);     // quieto: se queda con la ultima direccion
        }

        StepSprites set = vertical ? (up ? Fallback(back, front) : Fallback(front)) : Fallback(side);
        lastSet = set;
        return set;
    }

    // Si un juego de sprites esta vacio, usa otro que si tenga
    StepSprites Fallback(StepSprites wanted, StepSprites second = null)
    {
        if (wanted != null && wanted.HasAny) return wanted;
        if (second != null && second.HasAny) return second;
        if (side.HasAny) return side;
        if (front.HasAny) return front;
        return back;
    }

    void Show(Sprite sprite)
    {
        if (sprite == null) return;
        Sprite current = target.sprite;
        // El jugador no cambia de sprite por su cuenta (sin Animator), asi que con el no hace falta respetar a nadie
        if (respectOtherSprites && player == null && current != null && current != lastApplied && !IsMine(current)) return;
        if (current != sprite) target.sprite = sprite;
        lastApplied = sprite;
    }

    bool IsMine(Sprite s)
    {
        return Has(front, s) || Has(back, s) || Has(side, s);
    }

    static bool Has(StepSprites set, Sprite s) => set != null && (set.idle == s || set.step == s || set.dash == s);
}
