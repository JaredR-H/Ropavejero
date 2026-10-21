using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// Puerta de madera, caja, etc. que se parte con la rama.
/// Si 'Broken Sprite' está vacío, el objeto desaparece al romperse; si no, muestra ese sprite (escombros).
public class BreakableObject : MonoBehaviour, IHittable, IResettable
{
    [SerializeField] int hitsToBreak = 1;
    [SerializeField] SpriteRenderer sr;
    [SerializeField] Collider2D solidCollider;
    [SerializeField] Sprite brokenSprite;
    [SerializeField] UnityEvent onBroken;

    Color baseColor = Color.white;
    int hits;
    bool broken;
    SpriteSwapper swapper;

    void Awake()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (solidCollider == null) solidCollider = GetComponent<Collider2D>();
        if (sr != null) baseColor = sr.color;
        swapper = new SpriteSwapper(sr);
    }

    public void OnHit(PlayerAttack attacker)
    {
        if (broken) return;
        hits++;
        if (hits >= hitsToBreak) Break();
        else StartCoroutine(Flash());
    }

    void Break()
    {
        BreakSilently();
        onBroken.Invoke();
    }

    /// Rompe el objeto sin disparar eventos.
    public void BreakSilently()
    {
        broken = true;
        if (solidCollider != null) solidCollider.enabled = false;
        if (sr == null) return;

        if (brokenSprite != null)
        {
            sr.enabled = true;
            sr.sprite = brokenSprite;
        }
        else
        {
            sr.enabled = false;
        }
    }

    // Destello blanco breve al recibir un golpe que aún no lo rompe.
    IEnumerator Flash()
    {
        if (sr != null) sr.color = Color.white;
        yield return new WaitForSeconds(0.08f);
        if (sr != null) sr.color = baseColor;
    }

    public void ResetState()
    {
        StopAllCoroutines();
        hits = 0;
        broken = false;
        if (solidCollider != null) solidCollider.enabled = true;
        if (sr != null)
        {
            sr.enabled = true;
            sr.color = baseColor;
        }
        if (swapper != null) swapper.Show(null);   // vuelve al sprite original
    }
}
