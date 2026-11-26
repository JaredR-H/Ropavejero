using System;
using System.Collections;
using UnityEngine;

/// Se acabo el tiempo: la musica se apaga rapido, la camara hace zoom al jugador (RoomCompleteCinematic)
/// y el saco del Ropavejero le cae encima desde arriba con squash & stretch. Al terminar avisa al GameManager,
/// que hace el fundido a negro y muestra el Game Over (o vuelve al checkpoint del jefe).
/// Va en el mismo objeto que el GameManager (o en cualquiera de la escena Game).
public class TimeUpSequence : MonoBehaviour
{
    [Header("Saco")]
    [Tooltip("Sprite del saco (puede ser el mismo del BossSack).")]
    [SerializeField] Sprite sackSprite;
    [Tooltip("Tamano del saco (escala del objeto).")]
    [SerializeField] float sackScale = 1.4f;
    [Tooltip("Ajuste de donde queda el saco respecto a los pies del jugador.")]
    [SerializeField] Vector2 sackOffset = new Vector2(0f, 0.35f);
    [Tooltip("Ordenes de dibujo por encima del jugador.")]
    [SerializeField] int sortingAbovePlayer = 5;
    [Tooltip("Material del saco. Vacio = el mismo que el sprite del jugador.")]
    [SerializeField] Material sackMaterial;

    [Header("Tiempos (segundos reales)")]
    [SerializeField] float musicFadeOut = 0.4f;
    [Tooltip("Espera con el zoom puesto antes de que caiga el saco (el jugador entiende lo que pasa).")]
    [SerializeField] float beforeDrop = 0.7f;
    [Tooltip("Desde que altura cae (unidades por encima del jugador).")]
    [SerializeField] float dropHeight = 6f;
    [SerializeField] float fallTime = 0.4f;
    [Tooltip("Rebote al caer: cuanto se aplasta (0.35 = 35%).")]
    [SerializeField, Range(0f, 0.6f)] float landSquash = 0.38f;
    [SerializeField] float bounceTime = 0.55f;
    [Tooltip("El saco se sacude (el jugador forcejea dentro) durante este tiempo.")]
    [SerializeField] float struggleTime = 1.1f;
    [SerializeField] float struggleAngle = 9f;
    [Tooltip("Quieto antes del fundido a negro.")]
    [SerializeField] float afterStruggle = 0.35f;

    [Header("Camara")]
    [SerializeField, Range(0.2f, 1f)] float zoom = 0.5f;
    [SerializeField, Range(0f, 1f)] float focusShake = 0.25f;
    [SerializeField, Range(0f, 1f)] float landShake = 0.7f;
    [SerializeField] float landDustScale = 1.6f;

    GameObject sack;
    bool focusing;

    /// Empieza la secuencia. 'onFinished' se llama al terminar (con el saco aun encima y el zoom puesto).
    public void Play(PlayerHealth player, Action onFinished)
    {
        StopAllCoroutines();
        StartCoroutine(Run(player, onFinished));
    }

    /// Quita el saco y el zoom (llamalo con la pantalla en negro).
    public void Cleanup()
    {
        StopAllCoroutines();
        if (sack != null) Destroy(sack);
        sack = null;
        if (focusing && RoomCompleteCinematic.Instance != null) RoomCompleteCinematic.Instance.EndFocusImmediate();
        focusing = false;
    }

    IEnumerator Run(PlayerHealth player, Action onFinished)
    {
        Sfx.StopMusic(musicFadeOut);
        Sfx.Play(SfxId.TimeUp);

        Vector3 feet = player != null ? player.transform.position : Vector3.zero;
        Vector3 center = feet + (Vector3)sackOffset;

        var cinematic = RoomCompleteCinematic.Instance;
        focusing = cinematic != null && cinematic.BeginFocus(center, zoom, focusShake, 0.05f);
        if (player != null && player.Controller != null) player.Controller.InputEnabled = false;

        yield return Wait(beforeDrop);

        // ---- El saco cae ----
        sack = new GameObject("TimeUpSack");
        var sr = sack.AddComponent<SpriteRenderer>();
        sr.sprite = sackSprite;
        SpriteRenderer playerSr = player != null ? player.VisualRenderer : null;
        if (playerSr != null)
        {
            sr.sortingLayerID = playerSr.sortingLayerID;
            sr.sortingOrder = playerSr.sortingOrder + sortingAbovePlayer;
            sr.sharedMaterial = sackMaterial != null ? sackMaterial : playerSr.sharedMaterial;
        }
        else if (sackMaterial != null) sr.sharedMaterial = sackMaterial;

        Transform tr = sack.transform;
        Vector3 baseScale = Vector3.one * sackScale;
        Vector3 top = center + Vector3.up * dropHeight;
        Sfx.Play(SfxId.SackFall, center);

        float t = 0f;
        while (t < fallTime)
        {
            t += Step();
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, fallTime));
            float e = k * k;                                           // acelera al caer
            tr.position = Vector3.LerpUnclamped(top, center, e);
            tr.localScale = new Vector3(baseScale.x * (1f - 0.18f * e), baseScale.y * (1f + 0.25f * e), 1f);   // se estira al caer
            yield return null;
        }

        // ---- Aterriza: atrapa al jugador ----
        tr.position = center;
        if (player != null) player.HideVisuals();
        Sfx.Play(SfxId.SackDrop, center);
        Vfx.Play(VfxKind.SpawnPoof, center, Vector2.up, landDustScale);
        if (GameFeel.Instance != null) GameFeel.Instance.AddShake(landShake, Vector2.down);

        t = 0f;
        while (t < bounceTime)
        {
            t += Step();
            float spring = Mathf.Exp(-t * 7f) * Mathf.Cos(t * 3.2f * Mathf.PI * 2f);
            tr.localScale = new Vector3(baseScale.x * (1f + landSquash * spring), baseScale.y * (1f - landSquash * spring), 1f);
            yield return null;
        }

        // ---- Forcejea dentro del saco ----
        t = 0f;
        float nextMash = 0f;
        while (t < struggleTime)
        {
            float dt = Step();
            t += dt;
            float fade = 1f - Mathf.Clamp01(t / struggleTime) * 0.6f;
            float wob = Mathf.Sin(t * 22f) * fade;
            tr.localRotation = Quaternion.Euler(0f, 0f, wob * struggleAngle);
            tr.localScale = new Vector3(baseScale.x * (1f + 0.06f * Mathf.Abs(wob)), baseScale.y * (1f - 0.05f * Mathf.Abs(wob)), 1f);
            tr.position = center + new Vector3(Mathf.Sin(t * 37f) * 0.04f * fade, 0f, 0f);
            nextMash -= dt;
            if (nextMash <= 0f)
            {
                nextMash = 0.22f;
                Sfx.Play(SfxId.SackMash, center, 0.7f * fade);
            }
            yield return null;
        }
        tr.localRotation = Quaternion.identity;
        tr.localScale = baseScale;
        tr.position = center;

        yield return Wait(afterStruggle);
        onFinished?.Invoke();
    }

    static float Step() => Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);

    static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Step();
            yield return null;
        }
    }
}
