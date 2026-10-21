using System.Collections.Generic;
using UnityEngine;

/// Puente entre el jugador y su Animator. Va en el Player raíz.
/// Parámetros que usa (solo escribe los que EXISTEN en tu Animator Controller, el resto se ignora):
///   Direction (Float): 0 = frente, 1 = espalda, 2 = perfil
///   IsMoving (Bool), IsAirborne (Bool), Dead (Bool), Attack (Trigger)
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] Animator animator;      // el Animator del hijo "Visual"

    static readonly int DirectionId = Animator.StringToHash("Direction");
    static readonly int IsMovingId = Animator.StringToHash("IsMoving");
    static readonly int IsAirborneId = Animator.StringToHash("IsAirborne");
    static readonly int DeadId = Animator.StringToHash("Dead");
    static readonly int AttackId = Animator.StringToHash("Attack");

    PlayerController controller;
    readonly HashSet<int> existing = new HashSet<int>();
    bool ready;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("PlayerAnimator: falta el Animator o su Animator Controller. El jugador no se animará.", this);
            return;
        }

        foreach (var p in animator.parameters) existing.Add(p.nameHash);
        ready = true;
    }

    void LateUpdate()
    {
        if (!ready) return;
        SetFloat(DirectionId, controller.AnimDirection);
        SetBool(IsMovingId, controller.IsMoving);
        SetBool(IsAirborneId, controller.IsAirborne);
    }

    public void PlayAttack()
    {
        if (ready && existing.Contains(AttackId)) animator.SetTrigger(AttackId);
    }

    public void CancelAttack()
    {
        if (ready && existing.Contains(AttackId)) animator.ResetTrigger(AttackId);
    }

    public void SetDead(bool dead) => SetBool(DeadId, dead);

    void SetFloat(int id, float value)
    {
        if (existing.Contains(id)) animator.SetFloat(id, value);
    }

    void SetBool(int id, bool value)
    {
        if (existing.Contains(id)) animator.SetBool(id, value);
    }
}
