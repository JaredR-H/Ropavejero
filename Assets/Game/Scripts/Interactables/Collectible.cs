using UnityEngine;

/// Objeto de la sala. Se recoge solo acercándose (trigger). Flota suavemente.
/// Collider: CircleCollider2D con Is Trigger y radio ~0.5. Debe estar DENTRO del objeto Room_X.
public class Collectible : MonoBehaviour, IResettable
{
    [SerializeField] float bobHeight = 0.12f;
    [SerializeField] float bobSpeed = 3f;

    Vector3 startLocalPos;
    Room room;

    public bool Collected { get; private set; }

    void Awake()
    {
        startLocalPos = transform.localPosition;
        room = GetComponentInParent<Room>();
        if (room == null)
            Debug.LogWarning("Collectible '" + name + "' no está dentro de ningún Room: no abrirá ninguna puerta.", this);
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
        if (room != null) room.OnCollectibleTaken();
    }

    /// Lo usa la fase de escape: el objeto ya se recogió antes, así que no vuelve a aparecer.
    public void MarkCollectedSilently()
    {
        Collected = true;
        gameObject.SetActive(false);
    }

    public void ResetState()
    {
        Collected = false;
        transform.localPosition = startLocalPos;
        gameObject.SetActive(true);
    }
}