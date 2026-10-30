using UnityEngine;

/// Camina hacia el jugador en todo momento, hasta que lo toque (lo mata) o sea derrotado.
public class MeleeEnemy : Enemy
{
    protected override Vector2 ComputeDesiredVelocity()
    {
        Vector2 toPlayer = PlayerPosition - rb.position;
        if (toPlayer.sqrMagnitude < 0.0001f) return Vector2.zero;

        FaceDirection(toPlayer);
        return toPlayer.normalized * moveSpeed;
    }
}
