using UnityEngine;

/// Muerte de un solo golpe. Debe estar en el mismo GameObject que el collider del cuerpo (Player raíz).
[RequireComponent(typeof(PlayerController))]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] SpriteRenderer visualRenderer;
    [Tooltip("Tiñe al jugador de rojo al morir. Desmárcalo si tienes animación de muerte.")]
    [SerializeField] bool tintOnDeath = true;
    [SerializeField] Color deathColor = new Color(1f, 0.25f, 0.25f);

    PlayerAttack attack;
    PlayerAnimator anim;
    Color baseColor = Color.white;

    public PlayerController Controller { get; private set; }
    public bool IsDead { get; private set; }

    void Awake()
    {
        Controller = GetComponent<PlayerController>();
        attack = GetComponent<PlayerAttack>();
        anim = GetComponent<PlayerAnimator>();
        if (visualRenderer != null) baseColor = visualRenderer.color;
    }

    public void Die()
    {
        if (IsDead) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        IsDead = true;
        if (attack != null) attack.CancelAttack();
        Controller.InputEnabled = false;
        if (tintOnDeath && visualRenderer != null) visualRenderer.color = deathColor;
        if (anim != null) anim.SetDead(true);

        if (GameManager.Instance != null) GameManager.Instance.OnPlayerDied();
    }

    public void Revive(Vector3 position)
    {
        IsDead = false;
        if (visualRenderer != null) visualRenderer.color = baseColor;
        if (anim != null) anim.SetDead(false);
        if (attack != null) attack.CancelAttack();
        Controller.TeleportTo(position);
        Controller.InputEnabled = true;
    }
}
