using System.Collections;
using UnityEngine;

/// Suelo inestable: normal -> (lo pisas) agrietado -> caído = hoyo mortal.
/// Si saltas por encima no se activa. Opcional: se recupera tras 'recoverTime' segundos (0 = nunca).
public class UnstableFloor : MonoBehaviour, IResettable
{
    enum State { Stable, Cracking, Fallen }

    [SerializeField] SpriteRenderer sr;
    [SerializeField] float crackTime = 1.2f;       // tiempo agrietándose antes de caer
    [SerializeField] float recoverTime = 0f;       // 0 = el hoyo es permanente

    [Header("Sprites por estado (vacío = sprite del SpriteRenderer)")]
    [SerializeField] Sprite stableSprite;
    [SerializeField] Sprite crackingSprite;
    [SerializeField] Sprite fallenSprite;

    State state = State.Stable;
    Coroutine routine;
    SpriteSwapper swapper;

    void Awake()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        swapper = new SpriteSwapper(sr);
    }

    void OnEnable() => ResetState();

    public void ResetState()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        SetState(State.Stable);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (state == State.Stable)
        {
            if (other.TryGetComponent(out PlayerController pc) && !pc.IsAirborne && isActiveAndEnabled)
                routine = StartCoroutine(FallRoutine());
        }
        else if (state == State.Fallen)
        {
            HazardUtil.TryKillPlayer(other);   // ignora al jugador si está en el aire
        }
    }

    IEnumerator FallRoutine()
    {
        SetState(State.Cracking);
        yield return new WaitForSeconds(crackTime);
        SetState(State.Fallen);

        if (recoverTime > 0f)
        {
            yield return new WaitForSeconds(recoverTime);
            SetState(State.Stable);
        }
        routine = null;
    }

    void SetState(State s)
    {
        state = s;
        if (swapper == null) return;
        swapper.Show(s == State.Fallen ? fallenSprite : (s == State.Cracking ? crackingSprite : stableSprite));
    }
}
