using UnityEngine;

public class FloorHazard : MonoBehaviour
{
    [Tooltip("Marcado: se puede saltar por encima. Desmarcado: mata aunque estés en el aire.")]
    [SerializeField] bool canJumpOver = true;

    // Stay (no Enter) para matar también si el jugador reaparece justo encima.
    void OnTriggerStay2D(Collider2D other)
    {
        HazardUtil.TryKillPlayer(other, canJumpOver);
    }
}
