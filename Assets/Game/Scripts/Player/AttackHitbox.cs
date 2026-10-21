using System.Collections.Generic;
using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    PlayerAttack owner;
    readonly HashSet<IHittable> alreadyHit = new HashSet<IHittable>();

    void Awake() => owner = GetComponentInParent<PlayerAttack>();

    void OnEnable() => alreadyHit.Clear();

    void OnTriggerEnter2D(Collider2D other)
    {
        var hittable = other.GetComponentInParent<IHittable>();
        if (hittable == null || !alreadyHit.Add(hittable)) return;
        hittable.OnHit(owner);
    }
}
