using UnityEngine;
[RequireComponent(typeof(PlayerController))]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] SpriteRenderer visualRenderer;
    [Tooltip("Tine al jugador de rojo al morir. Desmarcalo si tienes animacion de muerte.")]
    [SerializeField] bool tintOnDeath = true;
    [SerializeField] Color deathColor = new Color(1f, 0.25f, 0.25f);

    [Header("Golpe mortal")]
    [Tooltip("Fuerza con la que sale empujado el jugador al ser golpeado.")]
    [SerializeField] float deathKnockbackForce = 9f;

    PlayerAttack attack;
    PlayerAnimator anim;
    Animator visualAnimator;
    Color baseColor = Color.white;

    public PlayerController Controller { get; private set; }
    public bool IsDead { get; private set; }

    void Awake()
    {
        Controller = GetComponent<PlayerController>();
        attack = GetComponent<PlayerAttack>();
        anim = GetComponent<PlayerAnimator>();
        visualAnimator = GetComponentInChildren<Animator>(true);
        if (visualRenderer != null) baseColor = visualRenderer.color;
    }
    public void Die() => DieInternal(Vector2.zero, false);
    public void Die(Vector2 sourcePosition) => DieInternal(sourcePosition, true);

    void DieInternal(Vector2 source, bool hasSource)
    {
        if (IsDead) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
        if (Controller.IsInvulnerable) return;

        IsDead = true;

        Vector2 dir = hasSource ? (Vector2)transform.position - source : -Controller.LastMoveDirection;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
        dir.Normalize();

        if (attack != null) attack.CancelAttack();
        Controller.InputEnabled = false;
        Controller.ApplyKnockback(dir, deathKnockbackForce);

        if (tintOnDeath && visualRenderer != null) visualRenderer.color = deathColor;
        if (anim != null) anim.SetDead(true);
        SetAnimatorUnscaled(true);   // la animacion de muerte sigue durante el tiempo congelado

        if (GameManager.Instance != null) GameManager.Instance.OnPlayerDied(dir);
    }

    public void Revive(Vector3 position)
    {
        IsDead = false;
        if (visualRenderer != null) visualRenderer.color = baseColor;
        if (anim != null) anim.SetDead(false);
        SetAnimatorUnscaled(false);
        if (attack != null) attack.CancelAttack();
        Controller.TeleportTo(position);
        Controller.InputEnabled = true;
    }

    void SetAnimatorUnscaled(bool unscaled)
    {
        if (visualAnimator != null)
            visualAnimator.updateMode = unscaled ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
    }
}
