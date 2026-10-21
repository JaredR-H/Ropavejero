using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Emisor de flechas pegado a una pared. Cambia al sprite de aviso ('telegraphTime') antes de disparar.
public class ArrowTrap : MonoBehaviour, IResettable
{
    [SerializeField] Arrow arrowPrefab;
    [SerializeField] Transform spawnPoint;                 // hijo, ~0.8 unidades DENTRO de la sala
    [SerializeField] Vector2 direction = Vector2.right;    // (1,0) (-1,0) (0,1) (0,-1)
    [SerializeField] float arrowSpeed = 7f;
    [SerializeField] float startDelay = 1f;                // cámbialo por emisor para desfasarlos
    [SerializeField] float interval = 2.5f;
    [SerializeField] float telegraphTime = 0.5f;
    [SerializeField] SpriteRenderer emitterRenderer;

    [Header("Sprites del emisor (vacío = sprite del SpriteRenderer)")]
    [SerializeField] Sprite idleSprite;
    [SerializeField] Sprite telegraphSprite;               // se muestra justo antes de disparar

    readonly List<Arrow> liveArrows = new List<Arrow>();
    Coroutine loop;
    SpriteSwapper swapper;

    void Awake()
    {
        if (emitterRenderer == null) emitterRenderer = GetComponent<SpriteRenderer>();
        swapper = new SpriteSwapper(emitterRenderer);
    }

    void OnEnable() => ResetState();

    public void ResetState()
    {
        if (loop != null) StopCoroutine(loop);
        loop = null;

        foreach (var a in liveArrows)
            if (a != null) Destroy(a.gameObject);
        liveArrows.Clear();

        if (swapper != null) swapper.Show(idleSprite);
        if (isActiveAndEnabled) loop = StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        yield return new WaitForSeconds(startDelay);
        while (true)
        {
            swapper.Show(telegraphSprite);
            yield return new WaitForSeconds(telegraphTime);
            Fire();
            swapper.Show(idleSprite);
            yield return new WaitForSeconds(interval);
        }
    }

    void Fire()
    {
        liveArrows.RemoveAll(a => a == null);
        // Se instancia bajo el mismo padre que el emisor (p. ej. "Traps"), así se desactiva junto con la sala.
        var arrow = Instantiate(arrowPrefab, spawnPoint.position, Quaternion.identity, transform.parent);
        arrow.Launch(direction, arrowSpeed);
        liveArrows.Add(arrow);
    }

    void OnDrawGizmosSelected()
    {
        if (spawnPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(spawnPoint.position, (Vector3)direction.normalized * 4f);
    }
}
