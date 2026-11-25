using UnityEngine;

/// El conjunto de trampas de una sala. Va en el objeto contenedor ("Traps") que tiene TODAS las trampas como hijas.
/// - Cuando la sala queda COMPLETADA, el contenedor se apaga entero: las trampas desaparecen y no hacen dano.
///   Tambien al volver a entrar a una sala completada.
/// - Dificultad progresiva (opcional): cuantas trampas de la lista se activan depende de lo lejos que esta la sala del inicio.
public class TrapSet : MonoBehaviour, IResettable
{
    [Tooltip("Trampas opcionales (hijos del contenedor), de la mas facil a la mas dificil. Las que no estan en la lista siempre se activan.")]
    [SerializeField] GameObject[] scalableTraps;
    [Tooltip("Cuantas trampas de la lista se activan (en la sala mas facil, en la mas dificil). 0 y 0 = todas.")]
    [SerializeField] Vector2Int activeRange = Vector2Int.zero;

    Room room;
    int activeCount = int.MaxValue;

    void Awake()
    {
        room = GetComponentInParent<Room>();
    }

    /// La llama Room.ApplyDifficulty al cargar el mapa. t = 0 (facil) a 1 (dificil).
    public void ApplyDifficulty(float t)
    {
        if (scalableTraps == null || scalableTraps.Length == 0 || activeRange.y <= 0)
        {
            activeCount = int.MaxValue;
        }
        else
        {
            float v = Mathf.Lerp(activeRange.x, activeRange.y, t);
            activeCount = Mathf.Clamp(Mathf.RoundToInt(v), 0, scalableTraps.Length);
        }
        ApplyActive();
    }

    void ApplyActive()
    {
        if (scalableTraps == null) return;
        for (int i = 0; i < scalableTraps.Length; i++)
            if (scalableTraps[i] != null) scalableTraps[i].SetActive(i < activeCount);
    }

    /// Sala completada: todas las trampas fuera.
    public void Deactivate()
    {
        gameObject.SetActive(false);
    }

    public void ResetState()
    {
        if (room != null && room.IsCompleted)
        {
            gameObject.SetActive(false);        // sala completada: las trampas siguen apagadas
            return;
        }

        gameObject.SetActive(true);
        ApplyActive();
    }
}
