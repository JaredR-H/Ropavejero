using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Botones con squash & stretch: crecen al pasar el mouse y se aplastan al presionar y rebotan al soltar.
/// Va en el objeto del Button (junto a un JuiceAnimator; se anade solo si falta). Funciona con el juego en pausa.
/// UIJuiceSetup lo coloca solo en todos los botones de un Canvas.
public class JuiceButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] JuiceAnimator juice;

    [Header("Tamano")]
    [Tooltip("Cuanto crece el boton con el mouse encima.")]
    [SerializeField] float hoverScale = 1.1f;
    [Tooltip("Cuanto se encoge mientras se mantiene presionado.")]
    [SerializeField] float pressScale = 0.93f;
    [Tooltip("Que tan rapido cambia de tamano.")]
    [SerializeField] float scaleSpeed = 14f;

    [Header("Animaciones del perfil de Juice")]
    [SerializeField] string hoverShot = "ButtonHover";
    [SerializeField] string pressShot = "ButtonPress";
    [SerializeField] string releaseShot = "ButtonRelease";

    Selectable selectable;
    bool hovering;
    bool pressed;

    bool Interactable => selectable == null || selectable.interactable;

    void Awake()
    {
        selectable = GetComponent<Selectable>();
        EnsureJuice(null);
    }

    /// Asigna el perfil de curvas (lo usa UIJuiceSetup).
    public void Configure(JuiceProfile profile) => EnsureJuice(profile);

    void EnsureJuice(JuiceProfile profile)
    {
        if (juice == null) juice = GetComponent<JuiceAnimator>();
        if (juice == null) juice = gameObject.AddComponent<JuiceAnimator>();
        juice.Configure(profile, true, false);     // tiempo real (pausa) y sin el balanceo continuo
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        if (!Interactable) return;
        juice.Play(hoverShot);
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        pressed = false;
        Refresh();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!Interactable) return;
        pressed = true;
        juice.Play(pressShot);
        Refresh();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressed) return;
        pressed = false;
        juice.Play(releaseShot);
        Refresh();
    }

    void Refresh()
    {
        float target = 1f;
        if (Interactable)
        {
            if (pressed) target = pressScale;
            else if (hovering) target = hoverScale;
        }
        juice.SetScaleMultiplier(target, scaleSpeed);
    }

    void OnDisable()
    {
        hovering = false;
        pressed = false;
        if (juice != null) juice.SnapScale(1f);
    }
}
