using UnityEngine;

/// Rastro de polvo al caminar: suelta una nubecita a los pies cada cierta distancia recorrida.
/// Sirve para el jugador y para los enemigos. Va en el objeto raiz (el que tiene el Rigidbody2D).
[RequireComponent(typeof(Rigidbody2D))]
public class DustTrail : MonoBehaviour
{
    [Tooltip("Donde salen las nubes respecto al pivote (normalmente los pies).")]
    [SerializeField] Vector2 feetOffset = new Vector2(0f, -0.05f);
    [Tooltip("Cada cuantas unidades recorridas sale una nube. Menor = mas polvo.")]
    [SerializeField, Min(0.1f)] float stepDistance = 0.6f;
    [Tooltip("Por debajo de esta velocidad no sale polvo (asi no sale al quedarse casi quieto).")]
    [SerializeField] float minSpeed = 1f;
    [Tooltip("Tamano de cada nube (1 = el polvo normal).")]
    [SerializeField] float puffScale = 0.55f;

    Rigidbody2D rb;
    PlayerController player;      // opcional: si existe, no sale polvo en el aire (salto con C)
    Vector2 lastPos;
    float traveled;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        player = GetComponent<PlayerController>();
    }

    void OnEnable()
    {
        if (rb != null) lastPos = rb.position;
        traveled = 0f;
    }

    void FixedUpdate()
    {
        Vector2 pos = rb.position;
        float moved = (pos - lastPos).magnitude;
        lastPos = pos;

        bool grounded = player == null || !player.IsAirborne;
        Vector2 velocity = rb.linearVelocity;
        if (!grounded || velocity.magnitude < minSpeed || moved > 2f)   // saltos de posicion (reaparecer) no cuentan
        {
            traveled = stepDistance * 0.5f;     // al volver a caminar sale pronto la primera nube
            return;
        }

        traveled += moved;
        if (traveled < stepDistance) return;

        traveled = 0f;
        Vfx.Play(VfxKind.Dust, pos + feetOffset, -velocity, puffScale);
    }
}
