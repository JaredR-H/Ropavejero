// Interactables.
public interface IHittable
{
    void OnHit(PlayerAttack attacker);
}

// Reinicio de hazards.
public interface IResettable
{
    void ResetState();
}
