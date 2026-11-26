using UnityEngine;
using UnityEngine.InputSystem;

/// Capa de parallax del menu: se desplaza un poquito segun la posicion del mouse.
/// Las capas lejanas (fondo) se mueven poco y las cercanas (personaje) mas: asi se nota la profundidad.
/// Va en un objeto PADRE de cada capa (no en el sprite que anima MainMenuIntro), para que no se pisen.
/// Funciona con sprites (unidades del mundo) y con UI (RectTransform, en pixeles).
public class MenuParallaxLayer : MonoBehaviour
{
    /// Multiplica todas las capas (MainMenuIntro lo sube de 0 a 1 al terminar la intro).
    public static float GlobalWeight = 1f;

    [Tooltip("Desplazamiento maximo con el mouse en una esquina. Sprites: unidades (0.1 a 0.5). UI: pixeles (5 a 25).")]
    [SerializeField] Vector2 maxOffset = new Vector2(0.25f, 0.12f);
    [Tooltip("Marcado: la capa se mueve AL CONTRARIO del mouse (como si la camara mirara hacia el mouse). Lo normal.")]
    [SerializeField] bool invert = true;
    [Tooltip("Inclinacion maxima en grados (0 = sin rotar). Un poquito en el personaje da mucha vida.")]
    [SerializeField] float maxTilt = 0f;
    [Tooltip("Suavizado: segundos que tarda en alcanzar al mouse. Mayor = mas lento y pesado.")]
    [SerializeField, Min(0.01f)] float smoothTime = 0.35f;

    RectTransform rect;
    Vector3 basePos;
    Quaternion baseRot;
    Vector2 current, velocity;

    void Awake()
    {
        rect = transform as RectTransform;
        basePos = rect != null ? (Vector3)rect.anchoredPosition : transform.localPosition;
        baseRot = transform.localRotation;
    }

    void LateUpdate()
    {
        Vector2 target = MouseNormalized() * GlobalWeight * (invert ? -1f : 1f);
        current = Vector2.SmoothDamp(current, target, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        Vector3 offset = new Vector3(current.x * maxOffset.x, current.y * maxOffset.y, 0f);
        if (rect != null) rect.anchoredPosition = basePos + offset;
        else transform.localPosition = basePos + offset;

        if (maxTilt != 0f) transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, -current.x * maxTilt);
    }

    /// Mouse de -1 a 1 respecto al centro de la pantalla (0 si no hay mouse).
    public static Vector2 MouseNormalized()
    {
        var mouse = Mouse.current;
        if (mouse == null || Screen.width <= 0 || Screen.height <= 0) return Vector2.zero;
        Vector2 p = mouse.position.ReadValue();
        Vector2 n = new Vector2(p.x / Screen.width, p.y / Screen.height) * 2f - Vector2.one;
        return Vector2.ClampMagnitude(new Vector2(Mathf.Clamp(n.x, -1f, 1f), Mathf.Clamp(n.y, -1f, 1f)), 1.42f);
    }
}
