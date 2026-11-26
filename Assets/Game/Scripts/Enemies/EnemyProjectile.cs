using UnityEngine;

/// Proyectil de un enemigo a distancia. Prefab: Rigidbody2D + CircleCollider2D con Is Trigger + este script + sprite.
/// Mata al jugador al tocarlo y desaparece al chocar con las paredes.
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] float lifetime = 4f;
    [Tooltip("Marcado: el jugador se salva si esta en el aire (salto con C).")]
    [SerializeField] bool canBeJumpedOver = false;
    [Tooltip("Capas contra las que el proyectil desaparece (Walls).")]
    [SerializeField] LayerMask destroyOnLayers;
    [Tooltip("Gira el sprite hacia donde va. Dibujalo apuntando a la DERECHA.")]
    [SerializeField] bool rotateToDirection = true;
    [Tooltip("Marcalo si el dibujo mira a la IZQUIERDA (p. ej. el pajaro furioso del Coyongo).")]
    [SerializeField] bool artFacesLeft = false;
    [Tooltip("Nunca queda de cabeza: voltea el sprite hacia el lado al que va y solo se inclina hacia arriba o abajo.")]
    [SerializeField] bool keepUpright = false;
    [Tooltip("Inclinacion maxima (grados) con Keep Upright. 90 = apunta exacto a donde va.")]
    [SerializeField, Range(0f, 90f)] float maxTilt = 60f;
    [Tooltip("Tamano de las chispas al chocar con una pared o con el jugador.")]
    [SerializeField] float impactScale = 0.7f;

    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    public void Launch(Vector2 direction, float speed)
    {
        direction.Normalize();
        rb.linearVelocity = direction * speed;
        if (!rotateToDirection) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (keepUpright)
        {
            bool goingLeft = direction.x < 0f;
            if (spriteRenderer != null) spriteRenderer.flipX = goingLeft != artFacesLeft;
            float tilt = Mathf.DeltaAngle(0f, goingLeft ? angle - 180f : angle);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(tilt, -maxTilt, maxTilt));
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, 0f, angle + (artFacesLeft ? 180f : 0f));
        }
    }

    void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerHealth _))
        {
            HazardUtil.TryKillPlayer(other, transform.position, canBeJumpedOver);
            Vfx.Play(VfxKind.Impact, transform.position, -rb.linearVelocity, impactScale);
            Sfx.Play(SfxId.ProjectileImpact, transform.position);
            Destroy(gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & destroyOnLayers.value) != 0)
        {
            Vfx.Play(VfxKind.Impact, transform.position, -rb.linearVelocity, impactScale);
            Sfx.Play(SfxId.ProjectileImpact, transform.position);
            Destroy(gameObject);
        }
    }
}
