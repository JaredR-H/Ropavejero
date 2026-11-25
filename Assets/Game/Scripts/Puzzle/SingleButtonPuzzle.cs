using UnityEngine;

/// El puzzle mas simple: presionar UN boton completa la sala.
/// Montaje: un objeto vacio dentro del Room con este script, y arrastra el PuzzleButton.
public class SingleButtonPuzzle : RoomPuzzle
{
    [SerializeField] PuzzleButton button;

    protected override void Awake()
    {
        base.Awake();
        if (button != null) button.Pressed += OnPressed;
    }

    void OnDestroy()
    {
        if (button != null) button.Pressed -= OnPressed;
    }

    void OnPressed(PuzzleButton b)
    {
        if (IsRunning) Solve();
    }

    protected override void OnBegin()
    {
        if (button != null) button.SetInteractable(true);
    }

    protected override void OnResetPuzzle()
    {
        if (button != null) button.ResetButton();
    }

    protected override void ShowSolved()
    {
        if (button == null) return;
        button.SetPressed(true);
        button.SetInteractable(false);
    }
}
