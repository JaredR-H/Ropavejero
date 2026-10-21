using UnityEngine;

/// Flecha: rectángulo con hitbox (trigger) que viaja recto hasta chocar con una pared.
/// Prefab: Rigidbody2D Kinematic + BoxCollider2D (Is Trigger) + este script.
[RequireComponent(typeof(Rigidbody2D))]
public class Arrow : MonoBehaviour
{
    [SerializeField] LayerMask wallMask;
    [SerializeField] bool hitsAirbornePlayer = true;     // saltar no salva de las flechas

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
            Destroy(gameObject);
            return;
        }
        HazardUtil.TryKillPlayer(other, !hitsAirbornePlayer);
    }
}
