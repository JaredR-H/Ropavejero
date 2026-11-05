using UnityEngine;

public class Collectible : MonoBehaviour, IResettable
{
    [SerializeField] float bobHeight = 0.12f;
    [SerializeField] float bobSpeed = 3f;

    [Header("UI (objetos recogidos)")]
    [Tooltip("Sprite que aparece en la UI al recogerlo. Si esta vacio se usa el de su SpriteRenderer.")]
    [SerializeField] Sprite uiIcon;
    [Tooltip("Color del borde de su icono en la UI (por ejemplo, otro color para los perks).")]
    [SerializeField] Color uiBorderColor = Color.white;

    Vector3 startLocalPos;
    Room room;

    SpriteRenderer spriteRenderer;

    public bool Collected { get; private set; }
    public bool Banked { get; private set; }

    public Color UiBorderColor => uiBorderColor;

    public Sprite UiIcon
    {
        get
        {
            if (uiIcon != null) return uiIcon;
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            return spriteRenderer != null ? spriteRenderer.sprite : null;
        }
    }

    void Awake()
    {
        startLocalPos = transform.localPosition;
        room = GetComponentInParent<Room>();
        if (room == null)
            Debug.LogWarning("Collectible '" + name + "' no esta dentro de ningun Room: no abrira ninguna puerta.", this);
    }

    void Update()
    {
        transform.localPosition = startLocalPos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (Collected) return;
        if (!other.TryGetComponent(out PlayerController _)) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        Collected = true;
        gameObject.SetActive(false);
        if (GameManager.Instance != null) GameManager.Instance.OnCollectibleCollected(this);
        if (room != null) room.OnCollectibleTaken();
    }

    /// Guarda el objeto como recogido de forma permanente (lo llama Room al salir con vida).
    public void Bank()
    {
        if (Collected) Banked = true;
    }

    public void ResetState()
    {
        if (Banked) return;     // ya esta guardado: se queda recogido

        if (Collected && GameManager.Instance != null) GameManager.Instance.OnCollectibleRestored(this);
        Collected = false;
        transform.localPosition = startLocalPos;
        gameObject.SetActive(true);
    }
}
