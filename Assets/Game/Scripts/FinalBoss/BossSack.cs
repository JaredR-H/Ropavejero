using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// El saco del Ropavejero. Lo maneja BossController: vuela en arco hasta un punto, atrapa al jugador si esta dentro
/// del circulo, el jugador mashea WASD para salir (o muere) y el saco vuelve al jefe.
/// Prefab: este script + un hijo "Visual" con el SpriteRenderer del saco (y un JuiceAnimator) + un hijo "Lever"
/// con el SpriteRenderer de la palanca que avisa que hay que mashear. Ponlo dentro de la sala del jefe (no dentro del jefe).
public class BossSack : MonoBehaviour
{
    [Header("Partes")]
    [Tooltip("SpriteRenderer del saco.")]
    [SerializeField] SpriteRenderer sackRenderer;
    [Tooltip("Opcional: JuiceAnimator del saco (squash & stretch). Vacio = se busca o se crea en el sprite.")]
    [SerializeField] JuiceAnimator sackJuice;
    [Tooltip("SpriteRenderer de la palanca (aviso de 'mashea').")]
    [SerializeField] SpriteRenderer leverRenderer;
    [Tooltip("Frames de la palanca moviendose de lado a lado (por ejemplo: izquierda, centro, derecha, centro).")]
    [SerializeField] Sprite[] leverFrames;
    [SerializeField] float leverFps = 10f;
    [Tooltip("Donde aparece la palanca respecto al saco.")]
    [SerializeField] Vector2 leverOffset = new Vector2(1.3f, 0.6f);

    [Header("Vuelo")]
    [Tooltip("Altura del arco al lanzarlo y al volver.")]
    [SerializeField] float arcHeight = 2f;

    [Header("Atrapar")]
    [Tooltip("Donde queda el jugador respecto al centro del saco (sus pies).")]
    [SerializeField] Vector2 holdOffset = new Vector2(0f, -0.2f);
    [Tooltip("Cuantas teclas hay que presionar para escapar.")]
    [SerializeField, Min(1)] int pressesToEscape = 12;
    [Tooltip("Segundos para escapar. Si se acaban, el jugador muere.")]
    [SerializeField] float escapeTime = 3.5f;
    [Tooltip("Shake de camara en cada tecla (GameFeel lo eleva al cuadrado).")]
    [SerializeField, Range(0f, 1f)] float mashShake = 0.3f;
    [Tooltip("Fuerza con la que sale despedido el jugador al escapar.")]
    [SerializeField] float ejectForce = 9f;
    [Tooltip("Segundos de invulnerabilidad al escapar.")]
    [SerializeField] float escapeInvulnerability = 1.2f;

    PlayerController heldPlayer;
    PlayerHealth heldHealth;
    PlayerAttack heldAttack;
    int baseSortingOrder;
    float leverTime;
    bool showLever;

    public bool IsOut { get; private set; }

    static bool GameIsPlaying => GameManager.Instance == null || GameManager.Instance.IsPlaying;

    void Awake()
    {
        if (sackRenderer == null) sackRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (sackJuice == null && sackRenderer != null)
        {
            sackJuice = sackRenderer.GetComponent<JuiceAnimator>();
            if (sackJuice == null) sackJuice = sackRenderer.gameObject.AddComponent<JuiceAnimator>();
            sackJuice.Configure(null, false, false);
        }
        if (sackRenderer != null) baseSortingOrder = sackRenderer.sortingOrder;
        HideNow();
    }

    void Update()
    {
        if (leverRenderer == null) return;
        leverRenderer.enabled = showLever;
        if (!showLever) return;

        leverRenderer.transform.position = (Vector2)transform.position + leverOffset;
        if (leverFrames != null && leverFrames.Length > 0)
        {
            leverTime += Time.deltaTime;
            int frame = Mathf.FloorToInt(leverTime * leverFps) % leverFrames.Length;
            leverRenderer.sprite = leverFrames[frame];
        }
    }

    // ---------- Vuelo ----------
    /// Vuela en arco de 'from' a 'to' en 'duration' segundos (se pausa si el juego no esta en marcha).
    public IEnumerator Fly(Vector2 from, Vector2 to, float duration)
    {
        IsOut = true;
        gameObject.SetActive(true);
        if (sackRenderer != null) sackRenderer.enabled = true;

        float t = 0f;
        duration = Mathf.Max(0.05f, duration);
        while (t < duration)
        {
            if (GameIsPlaying) t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            Vector2 p = Vector2.Lerp(from, to, k) + Vector2.up * (4f * k * (1f - k) * arcHeight);
            transform.position = p;
            yield return null;
        }
        transform.position = to;
    }

    /// Golpe al caer al suelo: polvo, squash y shake.
    public void Land(float shake)
    {
        if (sackJuice != null) sackJuice.Play("Release");
        Vfx.Play(VfxKind.Dust, transform.position, Vector2.up, 1.6f);
        if (GameFeel.Instance != null && shake > 0f) GameFeel.Instance.AddShake(shake);
    }

    /// Vuelve al jefe y se esconde.
    public IEnumerator ReturnTo(Transform boss, float duration)
    {
        Vector2 from = transform.position;
        float t = 0f;
        duration = Mathf.Max(0.05f, duration);
        while (t < duration)
        {
            if (GameIsPlaying) t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            Vector2 to = boss != null ? (Vector2)boss.position : from;
            transform.position = Vector2.Lerp(from, to, k) + Vector2.up * (4f * k * (1f - k) * arcHeight * 0.6f);
            yield return null;
        }
        HideNow();
    }

    // ---------- Atrapar ----------
    /// El jugador queda dentro del saco y tiene que mashear. onResult(true) = escapo, onResult(false) = murio.
    public IEnumerator Capture(PlayerController player, Vector2 bossPosition, Action<bool> onResult)
    {
        heldPlayer = player;
        heldHealth = player.GetComponent<PlayerHealth>();
        heldAttack = player.GetComponent<PlayerAttack>();

        Vector2 holdPos = (Vector2)transform.position + holdOffset;
        player.Hold(true, holdPos);
        if (heldHealth != null) heldHealth.SetCaptured(true);
        if (heldAttack != null)
        {
            heldAttack.CancelAttack();
            heldAttack.ForceHidden = true;     // la espada no se ve dentro del saco
        }

        // El saco por delante del jugador
        if (sackRenderer != null && heldHealth != null && heldHealth.VisualRenderer != null)
        {
            sackRenderer.sortingLayerID = heldHealth.VisualRenderer.sortingLayerID;
            sackRenderer.sortingOrder = heldHealth.VisualRenderer.sortingOrder + 3;
            if (leverRenderer != null)
            {
                leverRenderer.sortingLayerID = sackRenderer.sortingLayerID;
                leverRenderer.sortingOrder = sackRenderer.sortingOrder + 1;
            }
        }

        showLever = true;
        leverTime = 0f;
        if (sackJuice != null) sackJuice.Play("SackStruggle");

        int presses = 0;
        float t = 0f;
        bool escaped = false;
        while (t < escapeTime)
        {
            if (GameIsPlaying)
            {
                t += Time.deltaTime;
                if (MashPressedThisFrame())
                {
                    presses++;
                    if (sackJuice != null) sackJuice.Play("SackStruggle", Vector2.zero, 0f, 1f + 0.06f * presses);
                    if (GameFeel.Instance != null) GameFeel.Instance.AddShake(mashShake, UnityEngine.Random.insideUnitCircle);
                    if (presses % 3 == 0) Vfx.Play(VfxKind.Dust, transform.position, Vector2.up, 0.6f);
                    if (presses >= pressesToEscape)
                    {
                        escaped = true;
                        break;
                    }
                }
            }
            yield return null;
        }

        showLever = false;
        ReleasePlayer();

        if (escaped)
        {
            Vector2 away = (Vector2)player.transform.position - bossPosition;
            if (away.sqrMagnitude < 0.01f) away = Vector2.down;
            player.ApplyKnockback(away.normalized, ejectForce);
            if (heldHealth != null) heldHealth.GrantInvulnerability(escapeInvulnerability);
            Vfx.Play(VfxKind.Dust, transform.position, Vector2.up, 1.4f);
            if (sackJuice != null) sackJuice.Play("Release");
            if (GameFeel.Instance != null) GameFeel.Instance.AddShake(0.45f);
        }
        else if (heldHealth != null)
        {
            heldHealth.ForceDie(transform.position);
        }

        heldPlayer = null;
        heldHealth = null;
        heldAttack = null;
        onResult?.Invoke(escaped);
    }

    static bool MashPressedThisFrame()
    {
        var kb = Keyboard.current;
        if (kb == null) return false;
        return kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame
            || kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame
            || kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
    }

    void ReleasePlayer()
    {
        if (heldPlayer != null && heldPlayer.IsHeld) heldPlayer.Hold(false);
        if (heldHealth != null) heldHealth.SetCaptured(false);
        if (heldAttack != null) heldAttack.ForceHidden = false;
        if (sackRenderer != null) sackRenderer.sortingOrder = baseSortingOrder;
    }

    /// Lo esconde al instante y suelta al jugador si lo tenia (reinicio de la pelea).
    public void HideNow()
    {
        StopAllCoroutines();
        ReleasePlayer();
        heldPlayer = null;
        heldHealth = null;
        heldAttack = null;
        showLever = false;
        if (leverRenderer != null) leverRenderer.enabled = false;
        IsOut = false;
        if (sackRenderer != null) sackRenderer.enabled = false;
    }
}
