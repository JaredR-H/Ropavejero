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

    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    public void Launch(Vector2 direction, float speed)
    {
        direction.Normalize();
        rb.linearVelocity = direction * speed;
        if (rotateToDirection)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
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
            Destroy(gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & destroyOnLayers.value) != 0)
            Destroy(gameObject);
    }
}
