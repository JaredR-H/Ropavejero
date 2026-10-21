using UnityEngine;
using UnityEngine.Events;

/// Botón (se activa una vez) o palanca (alterna) que se acciona con el golpe de la rama.
/// Conecta los eventos en el inspector, p. ej. onActivated -> Openable.Open().
public class HitSwitch : MonoBehaviour, IHittable, IResettable
{
    [SerializeField] bool toggle = false;              // false = botón, true = palanca
    [SerializeField] SpriteRenderer sr;

    [Header("Sprites (vacío = sprite del SpriteRenderer)")]
    [SerializeField] Sprite offSprite;                 // sin activar
    [SerializeField] Sprite onSprite;                  // activado

    [SerializeField] UnityEvent onActivated;
    [SerializeField] UnityEvent onDeactivated;

    bool isOn;
    SpriteSwapper swapper;

    void Awake()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        swapper = new SpriteSwapper(sr);
        Apply();
    }

    public void OnHit(PlayerAttack attacker)
    {
        if (isOn && !toggle) return;
        isOn = !isOn;
        Apply();
        if (isOn) onActivated.Invoke();
        else onDeactivated.Invoke();
    }

    public void ResetState()
    {
        isOn = false;
        Apply();
    }

    void Apply()
    {
        if (swapper != null) swapper.Show(isOn ? onSprite : offSprite);
    }
}
