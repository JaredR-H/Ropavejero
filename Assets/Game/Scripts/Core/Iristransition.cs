using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Transición tipo caricatura: un círculo se cierra (o se abre) alrededor de un objetivo (el jugador).
/// Va en el Image "IrisOverlay" (último hijo del Canvas, con el material IrisWipe).
public class IrisTransition : MonoBehaviour
{
    [SerializeField] Image image;
    [SerializeField] Camera cam;
    [SerializeField] float openRadius = 2.5f;                          // suficiente para cubrir toda la pantalla
    [SerializeField] Vector3 worldOffset = new Vector3(0f, 0.5f, 0f);  // centro del cuerpo, no los pies

    const float ClosedRadius = -0.01f;   // ligeramente negativo para que cierre del todo (sin punto abierto)

    static readonly int CenterId = Shader.PropertyToID("_Center");
    static readonly int RadiusId = Shader.PropertyToID("_Radius");
    static readonly int AspectId = Shader.PropertyToID("_Aspect");

    Material mat;

    void Awake()
    {
        if (image == null) image = GetComponent<Image>();
        if (cam == null) cam = Camera.main;

        mat = new Material(image.material);   // copia, para no modificar el asset del material
        image.material = mat;
        image.raycastTarget = false;          // que no bloquee los botones de la UI
        SetRadius(openRadius);
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }

    /// Deja la pantalla completamente tapada (para empezar el juego con un "abrir").
    public void SnapClosed(Transform target)
    {
        UpdateCenter(target);
        SetRadius(ClosedRadius);
    }

    public IEnumerator Close(Transform target, float duration) => Animate(openRadius, ClosedRadius, duration, target);
    public IEnumerator Open(Transform target, float duration) => Animate(ClosedRadius, openRadius, duration, target);

    IEnumerator Animate(float from, float to, float duration, Transform target)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float n = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            UpdateCenter(target);
            SetRadius(Mathf.Lerp(from, to, n));
            yield return null;
        }
        UpdateCenter(target);
        SetRadius(to);
    }

    void UpdateCenter(Transform target)
    {
        Vector3 vp = cam.WorldToViewportPoint(target.position + worldOffset);
        mat.SetVector(CenterId, new Vector4(vp.x, vp.y, 0f, 0f));
        mat.SetFloat(AspectId, (float)Screen.width / Screen.height);
    }

    void SetRadius(float r) => mat.SetFloat(RadiusId, r);
}