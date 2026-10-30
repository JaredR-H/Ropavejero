using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public class PlayerAttack : MonoBehaviour
{
    [SerializeField] AttackHitbox hitbox;
    [Tooltip("Que tan lejos de la mano aparece el hitbox, en la direccion del mouse.")]
    [SerializeField] float hitboxDistance = 0.9f;
    [Tooltip("Solo si no hay PlayerAim: altura de la mano.")]
    [SerializeField] Vector2 fallbackCenterOffset = new Vector2(0f, 0.4f);
    [SerializeField] float windUp = 0.05f;
    [SerializeField] float activeTime = 0.15f;
    [SerializeField] float cooldown = 0.25f;
    [SerializeField] float moveSpeedWhileAttacking = 0.4f;
    [SerializeField] SlashEffect slashEffect;
    [SerializeField] WeaponVisual weapon;

    PlayerController controller;
    PlayerAnimator playerAnimator;
    PlayerAim aim;
    bool attacking;
    float nextAttackTime;
    Coroutine routine;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        playerAnimator = GetComponent<PlayerAnimator>();
        aim = GetComponent<PlayerAim>();
        if (hitbox != null) hitbox.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!controller.InputEnabled || attacking || controller.IsAirborne || controller.IsDashing) return;
        if (Time.time < nextAttackTime) return;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        bool pressed = (kb != null && kb.jKey.wasPressedThisFrame)
                    || (mouse != null && mouse.leftButton.wasPressedThisFrame);

        if (pressed) routine = StartCoroutine(AttackRoutine());
    }

    IEnumerator AttackRoutine()
    {
        attacking = true;
        controller.SpeedMultiplier = moveSpeedWhileAttacking;

        Vector2 dir = aim != null ? aim.Direction : controller.FacingVector;
        Vector2 origin = aim != null ? aim.Origin : (Vector2)transform.position + fallbackCenterOffset;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        hitbox.transform.position = origin + dir * hitboxDistance;
        hitbox.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        if (playerAnimator != null) playerAnimator.PlayAttack();
        if (weapon != null) weapon.Swing(windUp + activeTime);

        yield return new WaitForSeconds(windUp);
        hitbox.gameObject.SetActive(true);
        if (slashEffect != null) slashEffect.Play(hitbox.transform.position, dir);
        yield return new WaitForSeconds(activeTime);
        hitbox.gameObject.SetActive(false);

        controller.SpeedMultiplier = 1f;
        attacking = false;
        nextAttackTime = Time.time + cooldown;
        routine = null;
    }

    public void CancelAttack()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        attacking = false;
        if (hitbox != null) hitbox.gameObject.SetActive(false);
        if (controller != null) controller.SpeedMultiplier = 1f;
        if (playerAnimator != null) playerAnimator.CancelAttack();
        if (weapon != null) weapon.CancelSwing();
        if (slashEffect != null) slashEffect.Stop();
    }
}
