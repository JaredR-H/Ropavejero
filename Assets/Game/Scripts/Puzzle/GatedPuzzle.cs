using UnityEngine;

/// Puzzle con rejas dentro de la sala: hay que presionar el/los boton(es) de DESBLOQUEO para abrir las rejas
/// y poder llegar al boton de COMPLETAR (que hasta entonces no responde).
/// Montaje: pon las rejas (RoomGate) de modo que cierren el camino al boton de completar.
public class GatedPuzzle : RoomPuzzle
{
    [Tooltip("Botones que abren las rejas. Hay que presionar todos.")]
    [SerializeField] PuzzleButton[] unlockButtons;
    [SerializeField] RoomGate[] gates;
    [Tooltip("El boton que completa la sala (detras de las rejas).")]
    [SerializeField] PuzzleButton completeButton;

    [Tooltip("Marcado: al abrirse las rejas, la camara hace zoom a ellas con barras negras (necesita RoomCompleteCinematic en la escena).")]
    [SerializeField] bool focusCameraOnGates = true;

    bool gatesOpen;
    bool opening;

    protected override void Awake()
    {
        base.Awake();
        if (unlockButtons != null)
            foreach (var b in unlockButtons) if (b != null) b.Pressed += OnUnlockPressed;
        if (completeButton != null) completeButton.Pressed += OnCompletePressed;
    }

    void OnDestroy()
    {
        if (unlockButtons != null)
            foreach (var b in unlockButtons) if (b != null) b.Pressed -= OnUnlockPressed;
        if (completeButton != null) completeButton.Pressed -= OnCompletePressed;
    }

    protected override void OnBegin()
    {
        SetGates(false, false);
        if (completeButton != null) completeButton.SetInteractable(false);   // todavia no responde
    }

    protected override void OnResetPuzzle()
    {
        opening = false;
        if (unlockButtons != null)
            foreach (var b in unlockButtons) if (b != null) b.ResetButton();
        if (completeButton != null)
        {
            completeButton.ResetButton();
            completeButton.SetInteractable(false);
        }
        SetGates(false, false);
    }

    protected override void ShowSolved()
    {
        if (unlockButtons != null)
            foreach (var b in unlockButtons)
            {
                if (b == null) continue;
                b.SetPressed(true);
                b.SetInteractable(false);
            }
        if (completeButton != null)
        {
            completeButton.SetPressed(true);
            completeButton.SetInteractable(false);
        }
        SetGates(true, false);
    }

    void OnUnlockPressed(PuzzleButton pressed)
    {
        if (!IsRunning || gatesOpen || opening) return;

        foreach (var b in unlockButtons)
            if (b != null && !b.IsPressed) return;

        // Mini cinematica: zoom a las rejas y se ven abrir. Sin cinematica, se abren al instante.
        var cinematic = RoomCompleteCinematic.Instance;
        if (focusCameraOnGates && cinematic != null && cinematic.TryPlayFocus(GatesCenter(), OpenGates))
        {
            opening = true;
            return;
        }
        OpenGates();
    }

    void OpenGates()
    {
        opening = false;
        if (!IsRunning) return;
        SetGates(true, true);
        if (completeButton != null) completeButton.SetInteractable(true);
    }

    Vector3 GatesCenter()
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        if (gates != null)
            foreach (var g in gates)
                if (g != null) { sum += g.transform.position; n++; }
        return n > 0 ? sum / n : transform.position;
    }

    void OnCompletePressed(PuzzleButton pressed)
    {
        if (IsRunning && gatesOpen) Solve();
    }

    void SetGates(bool open, bool animate)
    {
        gatesOpen = open;
        if (gates == null) return;
        foreach (var g in gates) if (g != null) g.SetOpen(open, animate);
    }
}
