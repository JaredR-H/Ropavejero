using System.Collections;
using UnityEngine;

/// Parpadeo en blanco de un sprite (al recibir un golpe, o como bomba).
/// Con "Flash Material" (material del shader Ropavejero/SpriteFlash) el sprite se ve BLANCO.
/// Sin el, usa un parpadeo de transparencia como solucion de emergencia.
public class SpriteFlasher : MonoBehaviour
{
    [SerializeField] SpriteRenderer sr;
    [Tooltip("Material con el shader Ropavejero/SpriteFlash (Flash Amount = 1).")]
    [SerializeField] Material flashMaterial;
    [Tooltip("Solo si no hay Flash Material: el sprite se pone de este color.")]
    [SerializeField] Color fallbackColor = new Color(1f, 1f, 1f, 0.35f);

    Material originalMaterial;
    Color originalColor;
    Coroutine routine;
    bool captured;

    void Awake() => Capture();

    void Capture()
    {
        if (captured) return;
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;
        originalMaterial = sr.sharedMaterial;
        originalColor = sr.color;
        captured = true;
    }

    /// Enciende o apaga el blanco.
    public void SetFlash(bool on)
    {
        Capture();
        if (sr == null) return;

        if (flashMaterial != null) sr.sharedMaterial = on ? flashMaterial : originalMaterial;
        else sr.color = on ? fallbackColor : originalColor;
    }

    /// Un destello corto.
    public void Flash(float duration)
    {
        Cancel();
        routine = StartCoroutine(FlashRoutine(duration));
    }

    /// Parpadea cada 'interval' segundos durante 'duration' (0 = hasta Cancel).
    public void Blink(float duration, float interval)
    {
        Cancel();
        routine = StartCoroutine(BlinkRoutine(duration, interval));
    }

    public void Cancel()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        SetFlash(false);
    }

    IEnumerator FlashRoutine(float duration)
    {
        SetFlash(true);
        yield return new WaitForSeconds(duration);   // con escala de tiempo: el blanco se mantiene durante el hitstop
        SetFlash(false);
        routine = null;
    }

    IEnumerator BlinkRoutine(float duration, float interval)
    {
        float t = 0f;
        bool on = false;
        while (duration <= 0f || t < duration)
        {
            on = !on;
            SetFlash(on);
            float wait = Mathf.Max(0.02f, interval);
            yield return new WaitForSeconds(wait);
            t += wait;
        }
        SetFlash(false);
        routine = null;
    }

    void OnDisable()
    {
        routine = null;
        if (captured) SetFlash(false);
    }
}
