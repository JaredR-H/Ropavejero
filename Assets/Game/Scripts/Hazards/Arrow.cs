using UnityEngine;

/// Flecha: rectángulo con hitbox (trigger) que viaja recto hasta chocar con una pared.
/// Prefab: Rigidbody2D Kinematic + BoxCollider2D (Is Trigger) + este script.
[RequireComponent(typeof(Rigidbody2D))]
public class Arrow : MonoBehaviour
{
    [SerializeField] LayerMask wallMask;
    [SerializeField] bool hitsAirbornePlayer = true;     // saltar no salva de las flechas
    [Tooltip("Tamano de las chispas al chocar con una pared.")]
    [SerializeField] float impactScale = 0.8f;

    Rigidbody2D rb;

    public void Launch(Vector2 direction, float speed)
    {
        rb = GetComponent<Rigidbody2D>();
        Vector2 dir = direction.normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);   // el sprite se dibuja apuntando a la derecha
        rb.linearVelocity = dir * speed;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & wallMask) != 0)
        {
            Vector2 v = rb != null ? rb.linearVelocity : Vector2.zero;
            Vfx.Play(VfxKind.Impact, transform.position, -v, impactScale);   // chispas hacia atras, como un rebote
            Sfx.Play(SfxId.ArrowImpact, transform.position);
            Destroy(gameObject);
            return;
        }
        HazardUtil.TryKillPlayer(other, !hitsAirbornePlayer);
    }
}
