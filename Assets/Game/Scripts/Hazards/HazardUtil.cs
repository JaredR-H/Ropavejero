using UnityEngine;

public static class HazardUtil
{
    /// Mata al jugador si el collider recibido es el de su CUERPO.
    /// (El hitbox de ataque no tiene PlayerHealth, así que la rama nunca activa trampas.)
    public static void TryKillPlayer(Collider2D other, bool ignoreIfAirborne = true)
    {
        if (!other.TryGetComponent(out PlayerHealth health)) return;
        if (ignoreIfAirborne && health.Controller.IsAirborne) return;
        health.Die();
    }
}
