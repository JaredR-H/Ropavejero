using UnityEngine;

/// Arma que lleva un enemigo (por ejemplo el arco del arquero): apunta SIEMPRE hacia el jugador, para que el jugador
/// sepa que le estan apuntando. Su JuiceAnimator hace de tensar el arco (BowDraw) y soltar (BowRelease).
/// Estructura:  Enemigo > BowPivot (este script) > Bow (SpriteRenderer dibujado apuntando a la DERECHA + JuiceAnimator)
public class EnemyWeaponVisual : MonoBehaviour
{
    [Tooltip("El SpriteRenderer del arma (hijo de este objeto).")]
    [SerializeField] SpriteRenderer weaponRenderer;
    [Tooltip("El sprite del enemigo, para ordenar el arma delante o detras de el.")]
    [SerializeField] SpriteRenderer ownerRenderer;
    [Tooltip("JuiceAnimator del sprite del arma. Si esta vacio se busca en el.")]
    [SerializeField] JuiceAnimator weaponJuice;
    [Tooltip("Altura de las manos respecto al pivote del enemigo.")]
    [SerializeField] Vector2 originOffset = new Vector2(0f, 0.4f);
    [Tooltip("Cuanto se aleja el arma del centro de las manos.")]
    [SerializeField] float distance = 0.5f;
    [Tooltip("Grados por segundo con los que gira para seguir al jugador (0 = instantaneo).")]
    [SerializeField] float turnSpeed = 540f;
    [SerializeField] bool flipWhenAimingLeft = true;

    Transform owner;
    PlayerHealth player;
    Collider2D playerCollider;
    float angle;
    bool initialized;

    void Awake()
    {
        owner = transform.parent != null ? transform.parent : transform;
        if (weaponJuice == null && weaponRenderer != null) weaponJuice = weaponRenderer.GetComponent<JuiceAnimator>();
    }

    void Start()
    {
        if (weaponRenderer != null) weaponRenderer.transform.localPosition = new Vector3(distance, 0f, 0f);
    }

    void OnEnable() => initialized = false;

    void LateUpdate()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerHealth>();
            if (player != null) playerCollider = player.GetComponent<Collider2D>();
        }

        Vector2 origin = (Vector2)owner.position + originOffset;
        transform.position = origin;

        if (player != null)
        {
            Vector2 target = playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)player.transform.position;
            Vector2 d = target - origin;
            if (d.sqrMagnitude > 0.0001f)
            {
                float desired = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                angle = (!initialized || turnSpeed <= 0f)
                    ? desired
                    : Mathf.MoveTowardsAngle(angle, desired, turnSpeed * Time.deltaTime);
                initialized = true;
            }
        }
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        if (weaponRenderer == null) return;
        if (flipWhenAimingLeft) weaponRenderer.flipY = Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) > 90f;

        if (ownerRenderer != null)
        {
            weaponRenderer.sortingLayerID = ownerRenderer.sortingLayerID;
            bool aimingUp = Mathf.Sin(angle * Mathf.Deg2Rad) > 0.35f;
            weaponRenderer.sortingOrder = ownerRenderer.sortingOrder + (aimingUp ? -1 : 1);
        }
    }

    /// El arquero empieza a apuntar: el arco se tensa durante 'duration' segundos.
    public void Draw(float duration)
    {
        if (weaponJuice != null) weaponJuice.Play("BowDraw", Vector2.zero, duration);
    }

    /// Dispara: el arco se aplasta y rebota.
    public void Release()
    {
        if (weaponJuice != null) weaponJuice.Play("BowRelease");
    }

    public void Cancel()
    {
        if (weaponJuice != null) weaponJuice.StopAll();
    }
}
