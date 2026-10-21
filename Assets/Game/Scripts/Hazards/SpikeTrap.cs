using System.Collections;
using UnityEngine;

/// Púas que se activan y desactivan solas, con tiempos aleatorios.
/// Cada estado muestra su propio sprite: Safe (escondidas), Warning (a punto de salir), Active (fuera, matan).
public class SpikeTrap : MonoBehaviour, IResettable
{
    enum State { Safe, Warning, Active }

    [SerializeField] SpriteRenderer sr;
    [SerializeField] Vector2 safeTimeRange = new Vector2(1.5f, 3f);    // min / max apagadas
    [SerializeField] Vector2 activeTimeRange = new Vector2(1f, 2f);    // min / max activas
    [SerializeField] float warningTime = 0.35f;                        // 0 = sin aviso

    [Header("Sprites por estado (vacío = sprite del SpriteRenderer)")]
    [SerializeField] Sprite safeSprite;
    [SerializeField] Sprite warningSprite;
    [SerializeField] Sprite activeSprite;

    State state = State.Safe;
    Coroutine loop;
    SpriteSwapper swapper;

    void Awake()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        swapper = new SpriteSwapper(sr);
    }

    void OnEnable() => ResetState();

    public void ResetState()
    {
        if (loop != null) StopCoroutine(loop);
        loop = null;
        SetState(State.Safe);
        if (isActiveAndEnabled) loop = StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        yield return new WaitForSeconds(Random.Range(0f, safeTimeRange.y));   // desfase inicial
        while (true)
        {
            if (warningTime > 0f)
            {
                SetState(State.Warning);
                yield return new WaitForSeconds(warningTime);
            }
            SetState(State.Active);
            yield return new WaitForSeconds(Random.Range(activeTimeRange.x, activeTimeRange.y));
            SetState(State.Safe);
            yield return new WaitForSeconds(Random.Range(safeTimeRange.x, safeTimeRange.y));
        }
    }

    void SetState(State s)
    {
        state = s;
        if (swapper == null) return;
        swapper.Show(s == State.Active ? activeSprite : (s == State.Warning ? warningSprite : safeSprite));
    }

    // Stay (no Enter) para matar también si las púas salen justo debajo del jugador.
    void OnTriggerStay2D(Collider2D other)
    {
        if (state == State.Active) HazardUtil.TryKillPlayer(other);
    }
}
