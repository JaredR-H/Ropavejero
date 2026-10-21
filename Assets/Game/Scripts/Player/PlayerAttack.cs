using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// Golpe con la rama. Controles: J o clic izquierdo.
/// Activa un hitbox (hijo "AttackHitbox") delante del jugador durante un instante.
[RequireComponent(typeof(PlayerController))]
public class PlayerAttack : MonoBehaviour
{
    [SerializeField] AttackHitbox hitbox;
    [SerializeField] float hitboxDistance = 0.7f;                         // qué tan lejos del jugador aparece
    [SerializeField] Vector2 hitboxCenterOffset = new Vector2(0f, 0.4f);  // altura de la "mano"
    [SerializeField] float windUp = 0.05f;
    [SerializeField] float activeTime = 0.15f;
    [SerializeField] float cooldown = 0.25f;
    [SerializeField] float moveSpeedWhileAttacking = 0.4f;
    [SerializeField] private SlashEffect slashEffect;

    PlayerController controller;
    PlayerAnimator playerAnimator;
    bool attacking;
    float nextAttackTime;
    Coroutine routine;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        playerAnimator = GetComponent<PlayerAnimator>();
        if (hitbox != null) hitbox.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!controller.InputEnabled || attacking || controller.IsAirborne) return;
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
        hitbox.transform.localPosition = hitboxCenterOffset + controller.FacingVector * hitboxDistance;
        if (playerAnimator != null) playerAnimator.PlayAttack();

        yield return new WaitForSeconds(windUp);
        hitbox.gameObject.SetActive(true);
        if (slashEffect != null)
            slashEffect.Play(hitbox.transform.position, controller.FacingVector);
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
        if (slashEffect != null) slashEffect.Stop();
    }
}
