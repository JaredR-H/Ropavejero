using UnityEngine;


[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ChaserEnemy : MonoBehaviour, IResettable
{
    [Header("Persecucion")]
    [Tooltip("Velocidad en unidades por segundo (el jugador se mueve a la suya, ajustalo para que se pueda escapar)")]
    [SerializeField] private float speed = 2.5f;
    [Tooltip("Segundos que espera sin moverse al entrar a la sala o al reaparecer")]
    [SerializeField] private float startDelay = 2f;

    [Header("Dano")]
    [Tooltip("Marcado: el jugador se salva si esta en el aire. Desmarcado: mata aunque salte")]
    [SerializeField] private bool canJumpOver = false;
    [Tooltip("Marcado: mientras espera (antes de empezar a moverse) no mata")]
    [SerializeField] private bool harmlessWhileWaiting = true;

    [Header("Visual (opcional)")]
    [SerializeField] private SpriteRenderer sr;
    [Tooltip("Voltea el sprite segun hacia donde se mueve. Dibuja el sprite mirando a la DERECHA")]
    [SerializeField] private bool faceMoveDirection = true;
    [Tooltip("Transparencia mientras espera, para que se note que aun esta dormido")]
    [Range(0f, 1f)]
    [SerializeField] private float waitingAlpha = 0.5f;

    private Rigidbody2D rb;
    private PlayerHealth player;
    private Vector2 startPosition;
    private float waitTimer;

    private bool Waiting => waitTimer > 0f;

    private static bool GameIsPlaying =>
        GameManager.Instance == null || GameManager.Instance.IsPlaying;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        GetComponent<Collider2D>().isTrigger = true;

        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        startPosition = transform.position;
    }

    private void OnEnable()
    {
        ResetState();
    }

    public void ResetState()
    {
        waitTimer = startDelay;
        transform.position = startPosition;
        if (rb != null)
        {
            rb.position = startPosition;
            rb.linearVelocity = Vector2.zero;
        }
        ApplyAlpha();
    }

    private void Update()
    {
        if (!Waiting || !GameIsPlaying) return;

        waitTimer -= Time.deltaTime;
        if (waitTimer <= 0f)
        {
            waitTimer = 0f;
            ApplyAlpha();
        }
    }

    private void FixedUpdate()
    {
        if (Waiting || !GameIsPlaying) return;

        if (player == null) player = FindFirstObjectByType<PlayerHealth>();
        if (player == null || player.IsDead) return;

        Vector2 target = player.transform.position;
        Vector2 next = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);

        if (faceMoveDirection && sr != null)
        {
            float dx = target.x - rb.position.x;
            if (Mathf.Abs(dx) > 0.05f) sr.flipX = dx < 0f;
        }

        rb.MovePosition(next);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Waiting && harmlessWhileWaiting) return;
        HazardUtil.TryKillPlayer(other, canJumpOver);
    }

    private void ApplyAlpha()
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = Waiting ? waitingAlpha : 1f;
        sr.color = c;
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        startDelay = Mathf.Max(0f, startDelay);
    }
}