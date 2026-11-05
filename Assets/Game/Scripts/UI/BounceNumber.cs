using TMPro;
using UnityEngine;

/// Texto con un numero que "rebota" (squash & stretch) cada vez que cambia. Sirve para el timer, las muertes,
/// las monedas, etc. Si ademas se activa el modo urgente, el texto se pone de otro color, crece un poco y rebota mas fuerte.
/// Va en el mismo objeto que el TextMeshPro (junto a un JuiceAnimator; UIManager los anade solo si faltan).
public class BounceNumber : MonoBehaviour
{
    [SerializeField] TMP_Text text;
    [SerializeField] JuiceAnimator juice;

    [Tooltip("Como se escribe el numero. {0} es el valor. Ejemplo: \"Muertes: {0}\".")]
    [SerializeField] string format = "{0}";

    [Header("Rebote al cambiar de numero")]
    [Tooltip("Nombre de la animacion del perfil de Juice (normal).")]
    [SerializeField] string bounceShot = "NumberBounce";
    [Tooltip("Nombre de la animacion cuando esta en modo urgente (mas notoria).")]
    [SerializeField] string urgentBounceShot = "NumberBounceStrong";
    [SerializeField] float bounceIntensity = 1f;

    [Header("Modo urgente (opcional)")]
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color urgentColor = new Color(1f, 0.3f, 0.3f);
    [Tooltip("Cuanto crece el texto en modo urgente (1 = nada).")]
    [SerializeField] float urgentScale = 1.18f;
    [Tooltip("Que tan rapido crece y vuelve a su tamano.")]
    [SerializeField] float scaleSpeed = 8f;

    int shown = int.MinValue;
    bool urgent;

    public bool IsUrgent => urgent;

    void Awake()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (juice == null) juice = GetComponent<JuiceAnimator>();
    }

    /// Para configurarlo por codigo (lo usa UIManager al crearlo solo).
    public void Configure(string newFormat, Color normal, Color urgentCol, float urgentScaleValue)
    {
        format = newFormat;
        normalColor = normal;
        urgentColor = urgentCol;
        urgentScale = urgentScaleValue;
        if (text != null) text.color = urgent ? urgentColor : normalColor;
    }

    /// Cambia el numero. Solo rebota si el valor es distinto al que ya se muestra (el primero no rebota).
    public void SetValue(int value)
    {
        if (value == shown) return;
        bool first = shown == int.MinValue;
        shown = value;

        if (text != null) text.text = string.Format(format, value);
        if (first) return;

        if (juice == null) juice = GetComponent<JuiceAnimator>();
        if (juice != null) juice.Play(urgent ? urgentBounceShot : bounceShot, Vector2.zero, 0f, bounceIntensity);
    }

    /// Escribe un texto fijo (por ejemplo "--") sin rebote. El siguiente SetValue vuelve a empezar sin rebote.
    public void SetText(string value)
    {
        shown = int.MinValue;
        if (text != null) text.text = value;
    }

    /// Modo urgente: cambia el color y crece (o vuelve a su tamano) con suavidad.
    public void SetUrgent(bool value)
    {
        if (value == urgent) return;
        urgent = value;

        if (text != null) text.color = urgent ? urgentColor : normalColor;
        if (juice == null) juice = GetComponent<JuiceAnimator>();
        if (juice != null) juice.SetScaleMultiplier(urgent ? urgentScale : 1f, scaleSpeed);
    }
}
