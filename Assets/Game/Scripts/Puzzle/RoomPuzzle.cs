using UnityEngine;
using UnityEngine.Events;

/// Base de los puzzles de una sala. Un puzzle es un objetivo de la sala (IRoomObjective): al resolverlo, la sala queda
/// COMPLETADA (se apagan las trampas, se abren las puertas y desaparece la cuenta regresiva).
/// Va en un objeto vacio dentro del Room. Los tipos: SingleButtonPuzzle, OrderedButtonsPuzzle, SequencePuzzle,
/// TimedButtonsPuzzle y GatedPuzzle.
public abstract class RoomPuzzle : MonoBehaviour, IRoomObjective, IResettable
{
    [Header("Dificultad")]
    [Tooltip("Marcado: los valores 'facil' y 'dificil' se mezclan segun lo lejos que esta la sala del inicio del mapa.")]
    [SerializeField] protected bool scaleWithDepth = true;
    [Tooltip("Si no escala con la profundidad: dificultad fija (0 = facil, 1 = dificil).")]
    [SerializeField, Range(0f, 1f)] protected float fixedDifficulty = 0f;

    [Header("Eventos")]
    [Tooltip("Se ejecuta al resolver el puzzle (por ejemplo, para un efecto o un sonido).")]
    [SerializeField] UnityEvent onSolved;

    protected Room room;
    bool done;
    bool running;

    public bool IsDone => done;
    protected bool IsRunning => running && !done;

    /// 0 = facil, 1 = dificil.
    protected float Difficulty => scaleWithDepth && room != null ? room.DifficultyT : fixedDifficulty;
    protected float Scale(float easy, float hard) => Mathf.Lerp(easy, hard, Difficulty);
    protected int ScaleInt(int easy, int hard) => Mathf.RoundToInt(Scale(easy, hard));

    protected virtual void Awake()
    {
        room = GetComponentInParent<Room>();
    }

    /// La llama Room cuando el jugador entra.
    public void Begin()
    {
        if (done || running) return;
        running = true;
        OnBegin();
    }

    protected abstract void OnBegin();
    protected abstract void OnResetPuzzle();
    protected abstract void ShowSolved();

    protected void Solve()
    {
        if (done) return;
        done = true;
        running = false;

        ShowSolved();
        onSolved?.Invoke();
        if (room != null) room.ObjectiveCompleted(this);
    }

    /// Reinicio de la sala: si ya esta completada se queda resuelto; si no, empieza de cero.
    public void ResetState()
    {
        StopAllCoroutines();
        running = false;

        if (room != null && room.IsCompleted)
        {
            done = true;
            ShowSolved();
        }
        else
        {
            done = false;
            OnResetPuzzle();
        }
    }
}
