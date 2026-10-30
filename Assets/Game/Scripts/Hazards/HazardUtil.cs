using UnityEngine;

public static class HazardUtil
{
    public static void TryKillPlayer(Collider2D other, bool ignoreIfAirborne = true)
    {
        if (!other.TryGetComponent(out PlayerHealth health)) return;
        if (ignoreIfAirborne && health.Controller.IsAirborne) return;
        health.Die();
    }

    public static void TryKillPlayer(Collider2D other, Vector2 sourcePosition, bool ignoreIfAirborne = true)
    {
        if (!other.TryGetComponent(out PlayerHealth health)) return;
        if (ignoreIfAirborne && health.Controller.IsAirborne) return;
        health.Die(sourcePosition);
    }
}
