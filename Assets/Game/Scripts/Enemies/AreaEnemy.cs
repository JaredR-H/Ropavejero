using System.Collections;
using UnityEngine;

/// Enemigo de area (bomba). Camina hacia el jugador; al llegar a cierta distancia se queda quieto, parpadea en blanco y explota.
/// Si lo derrotas, tambien parpadea en blanco unos segundos y explota. La explosion mata al jugador dentro del radio.
public class AreaEnemy : Enemy
{
    enum State { Chasing, Fuse }

    [Header("Bomba")]
    [Tooltip("Distancia al jugador a la que activa la bomba.")]
    [SerializeField] float triggerDistance = 1.8f;
    [Tooltip("Segundos parpadeando antes de explotar.")]
    [SerializeField] float fuseTime = 1.2f;
    [Tooltip("Intervalo del parpadeo al empezar y al final (se acelera).")]
    [SerializeField] float blinkSlow = 0.18f;
    [SerializeField] float blinkFast = 0.05f;
    [Tooltip("Radio de la explosion: todo el que este dentro muere.")]
    [SerializeField] float explosionRadius = 2.2f;
    [Tooltip("Prefab opcional de la explosion (animacion o particulas que se destruya sola).")]
    [SerializeField] GameObject explosionPrefab;
    [SerializeField] float explosionEffectLifetime = 1.5f;

    [Header("Explosion: sensacion")]
    [Tooltip("Intensidad de la vibracion de camara al explotar (0 = ninguna, 1 = maxima).")]
    [Range(0f, 1f)]
    [SerializeField] float explosionShake = 0.6f;
    [Tooltip("Segundos de pausa (hitstop) al explotar.")]
    [SerializeField] float explosionHitStop = 0.06f;
    [Tooltip("Que tan marcada es la animacion de carga (balanceo y squash & stretch) mientras se enciende la bomba.")]
    [SerializeField] float chargeIntensity = 1.5f;

    State state = State.Chasing;

    // Este enemigo mata con la explosion, no por contacto.
    protected override bool ContactKills => false;

    protected override void OnReset() => state = State.Chasing;

    protected override Vector2 ComputeDesiredVelocity()
    {
        if (state == State.Fuse) return Vector2.zero;

        Vector2 toPlayer = PlayerPosition - rb.position;
        if (toPlayer.sqrMagnitude < 0.0001f) return Vector2.zero;

        FaceDirection(toPlayer);
        return toPlayer.normalized * moveSpeed;
    }

    protected override void Tick(float dt)
    {
        if (state != State.Chasing) return;
        if (Vector2.Distance(PlayerPosition, rb.position) <= triggerDistance) StartFuse();
    }

    // Al derrotarlo no desaparece: enciende la bomba.
    protected override void Defeat(Vector2 direction)
    {
        defeated = true;     // ya no recibe mas golpes
        StartFuse();
    }

    void StartFuse()
    {
        if (state == State.Fuse) return;
        state = State.Fuse;
        if (juice != null) juice.Play("Charge", Vector2.zero, fuseTime, chargeIntensity);   // dura lo que la mecha
        StartCoroutine(FuseRoutine());
    }

    IEnumerator FuseRoutine()
    {
        float t = 0f;
        float nextToggle = 0f;
        bool on = false;

        while (t < fuseTime)
        {
            if (GameIsPlaying)
            {
                t += Time.deltaTime;
                if (t >= nextToggle)
                {
                    on = !on;
                    if (flasher != null) flasher.SetFlash(on);
                    nextToggle = t + Mathf.Lerp(blinkSlow, blinkFast, Mathf.Clamp01(t / fuseTime));
                }
            }
            yield return null;
        }

        if (flasher != null) flasher.SetFlash(false);
        Explode();
    }

    void Explode()
    {
        Vector2 center = transform.position;

        if (explosionPrefab != null)
            Destroy(Instantiate(explosionPrefab, center, Quaternion.identity), explosionEffectLifetime);
        if (GameFeel.Instance != null) GameFeel.Instance.Explosion(explosionShake, explosionHitStop);

        foreach (var col in Physics2D.OverlapCircleAll(center, explosionRadius))
            HazardUtil.TryKillPlayer(col, center, false);

        defeated = true;
        gameObject.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
