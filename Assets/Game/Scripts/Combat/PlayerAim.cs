using UnityEngine;
using UnityEngine.InputSystem;

/// Calcula hacia donde apunta el mouse, respecto a la "mano" del jugador. Va en el Player raiz.
/// Lo leen PlayerController (hacia donde mira), PlayerAttack (hitbox) y WeaponVisual (arma).
[DefaultExecutionOrder(-50)]
public class PlayerAim : MonoBehaviour
{
    [Tooltip("Altura de la mano respecto a los pies del jugador. De aqui salen el golpe y el arma.")]
    [SerializeField] Vector2 originOffset = new Vector2(0f, 0.4f);
    [SerializeField] Camera cam;

    PlayerController controller;

    public Vector2 Direction { get; private set; } = Vector2.down;
    public float Angle { get; private set; } = -90f;
    public Vector2 Origin => (Vector2)transform.position + originOffset;
    public Vector2 MouseWorld { get; private set; }

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        // Sin control (muerte, transiciones) se conserva la ultima direccion.
        if (controller != null && !controller.InputEnabled) return;

        var mouse = Mouse.current;
        if (mouse == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 screen = mouse.position.ReadValue();
        screen.z = Mathf.Abs(cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(screen);
        MouseWorld = world;

        Vector2 d = (Vector2)world - Origin;
        if (d.sqrMagnitude < 0.01f) return;

        Direction = d.normalized;
        Angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
    }
}
