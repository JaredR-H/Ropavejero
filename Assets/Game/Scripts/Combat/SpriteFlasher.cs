using System.Collections;
using UnityEngine;

public class SpriteFlasher : MonoBehaviour
{
    [SerializeField] SpriteRenderer sr;
    [Tooltip("Material con el shader Ropavejero/SpriteFlash. Si esta vacio se crea uno solo.")]
    [SerializeField] Material flashMaterial;
    [Tooltip("Solo si no hay shader: el sprite se pone de este color.")]
    [SerializeField] Color fallbackColor = new Color(1f, 1f, 1f, 0.35f);

    static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    static Material defaultMaterial;
    static bool defaultTried;

    Material originalMaterial;
    Color originalColor;
    Coroutine routine;
    MaterialPropertyBlock block;
    bool captured;

    static Material DefaultMaterial
    {
        get
        {
            if (!defaultTried)
            {
                defaultTried = true;
                Shader shader = Shader.Find("Ropavejero/SpriteFlash");
                if (shader != null)
                {
                    defaultMaterial = new Material(shader)
                    {
                        name = "SpriteFlash (auto)",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }
            return defaultMaterial;
        }
    }

    void Awake() => Capture();

    void Capture()
    {
        if (captured) return;
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;

        originalMaterial = sr.sharedMaterial;
        originalColor = sr.color;
        if (flashMaterial == null) flashMaterial = DefaultMaterial;
        captured = true;
    }

    /// Asigna el material del destello por codigo (si es null no cambia nada).
    public void SetMaterial(Material material)
    {
        if (material != null) flashMaterial = material;
    }

    /// Cuanto blanco tiene el sprite: 0 = nada, 1 = blanco total.
    public void SetFlashAmount(float amount)
    {
        Capture();
        if (sr == null) return;

        amount = Mathf.Clamp01(amount);

        if (flashMaterial != null)
        {
            if (amount <= 0.001f)
            {
                if (sr.sharedMaterial != originalMaterial) sr.sharedMaterial = originalMaterial;
                return;
            }

            if (sr.sharedMaterial != flashMaterial) sr.sharedMaterial = flashMaterial;
            if (block == null) block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);                 // conserva la textura del sprite
            block.SetFloat(FlashAmountId, amount);
            sr.SetPropertyBlock(block);
        }
        else
        {
            sr.color = Color.Lerp(originalColor, fallbackColor, amount);
        }
    }

    /// Enciende o apaga el blanco.
    public void SetFlash(bool on) => SetFlashAmount(on ? 1f : 0f);

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

    /// El blanco sigue una curva durante 'duration' segundos (eje X: 0 a 1 en el tiempo; eje Y: cuanto blanco).
    public void PlayCurve(AnimationCurve curve, float duration, bool unscaled = false)
    {
        Cancel();
        routine = StartCoroutine(CurveRoutine(curve, Mathf.Max(0.02f, duration), unscaled));
    }

    public void Cancel()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        SetFlashAmount(0f);
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

    IEnumerator CurveRoutine(AnimationCurve curve, float duration, bool unscaled)
    {
        float t = 0f;
        while (t < duration)
        {
            t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            SetFlashAmount(curve.Evaluate(Mathf.Clamp01(t / duration)));
            yield return null;
        }
        SetFlashAmount(0f);
        routine = null;
    }

    void OnDisable()
    {
        routine = null;
        if (captured) SetFlashAmount(0f);
    }
}
