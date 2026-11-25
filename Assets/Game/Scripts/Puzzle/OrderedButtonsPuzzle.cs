using System.Collections;
using UnityEngine;

/// Varios botones que hay que presionar EN ORDEN (1, 2, 3...). Si te equivocas, todos se reinician.
/// Montaje: arrastra los botones a la lista en el orden correcto. Los numeros se escriben solos si el boton tiene Label.
public class OrderedButtonsPuzzle : RoomPuzzle
{
    [Tooltip("Los botones, en el orden en que hay que presionarlos.")]
    [SerializeField] PuzzleButton[] buttons;
    [Tooltip("Escribe 1, 2, 3... sobre cada boton (si tiene Label).")]
    [SerializeField] bool showNumbers = true;
    [Tooltip("Dificultad progresiva: cuantos botones se usan (minimo, maximo). 0 y 0 = todos.")]
    [SerializeField] Vector2Int activeButtons = Vector2Int.zero;
    [Tooltip("Tiempo que se ve el error antes de reiniciar.")]
    [SerializeField] float wrongResetDelay = 0.5f;

    int expected;
    bool locked;

    int Count
    {
        get
        {
            if (buttons == null) return 0;
            if (activeButtons.y <= 0) return buttons.Length;
            return Mathf.Clamp(ScaleInt(activeButtons.x, activeButtons.y), 1, buttons.Length);
        }
    }

    protected override void Awake()
    {
        base.Awake();
        if (buttons == null) return;
        foreach (var b in buttons) if (b != null) b.Pressed += OnPressed;
    }

    void OnDestroy()
    {
        if (buttons == null) return;
        foreach (var b in buttons) if (b != null) b.Pressed -= OnPressed;
    }

    void ApplyConfig()
    {
        int count = Count;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            bool used = i < count;
            buttons[i].gameObject.SetActive(used);
            buttons[i].SetLabel(used && showNumbers ? (i + 1).ToString() : "");
        }
    }

    protected override void OnBegin()
    {
        ApplyConfig();
        expected = 0;
        locked = false;
    }

    protected override void OnResetPuzzle()
    {
        if (buttons == null) return;
        foreach (var b in buttons) if (b != null) b.ResetButton();
        expected = 0;
        locked = false;
        ApplyConfig();
    }

    protected override void ShowSolved()
    {
        if (buttons == null) return;
        ApplyConfig();
        foreach (var b in buttons)
        {
            if (b == null) continue;
            b.SetPressed(true);
            b.SetInteractable(false);
        }
    }

    void OnPressed(PuzzleButton pressed)
    {
        if (!IsRunning || locked) return;

        int index = System.Array.IndexOf(buttons, pressed);
        if (index == expected)
        {
            expected++;
            if (expected >= Count) Solve();
        }
        else
        {
            StartCoroutine(WrongRoutine());
        }
    }

    IEnumerator WrongRoutine()
    {
        locked = true;
        int count = Count;
        for (int i = 0; i < count; i++)
            if (buttons[i] != null) buttons[i].FlashWrong(wrongResetDelay);

        yield return new WaitForSeconds(wrongResetDelay);

        for (int i = 0; i < buttons.Length; i++)
            if (buttons[i] != null) buttons[i].ResetButton();
        expected = 0;
        locked = false;
    }
}
