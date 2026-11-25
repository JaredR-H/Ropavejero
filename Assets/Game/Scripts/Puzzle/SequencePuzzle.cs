using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Puzzle de secuencia (tipo Simon): la sala ILUMINA los botones en un orden y hay que repetirlo.
/// Si te equivocas, se vuelve a mostrar la secuencia. La secuencia queda igual hasta resolver la sala.
/// - Progressive desmarcado: se muestra la secuencia completa de una vez.
/// - Progressive marcado: se muestra 1, luego 2, luego 3... hasta la longitud final.
public class SequencePuzzle : RoomPuzzle
{
    [SerializeField] PuzzleButton[] buttons;
    [Tooltip("Dificultad progresiva: longitud de la secuencia (facil, dificil).")]
    [SerializeField] Vector2Int sequenceLength = new Vector2Int(3, 6);
    [SerializeField] bool progressive = false;

    [Header("Tiempos (segundos)")]
    [SerializeField] float startDelay = 0.9f;
    [Tooltip("Cuanto tiempo se ilumina cada boton al mostrar la secuencia.")]
    [SerializeField] float showStepTime = 0.55f;
    [SerializeField] float stepGap = 0.2f;
    [Tooltip("Espera tras un error antes de volver a mostrar la secuencia.")]
    [SerializeField] float replayDelay = 1f;

    readonly List<int> sequence = new List<int>();
    int round;
    int inputIndex;
    bool showing;

    bool HasButtons => buttons != null && buttons.Length > 0;

    protected override void Awake()
    {
        base.Awake();

        // Quita huecos vacios de la lista para que la secuencia nunca apunte a un boton que no existe
        if (buttons != null) buttons = System.Array.FindAll(buttons, b => b != null);
        if (!HasButtons)
        {
            Debug.LogError("SequencePuzzle en '" + (room != null ? room.name : name) + "': la lista 'Buttons' esta vacia. " +
                           "Arrastra los PuzzleButton de la sala. Mientras tanto el puzzle no se puede resolver.", this);
            return;
        }

        foreach (var b in buttons)
        {
            if (b == null) continue;
            b.SetLatch(false);          // en una secuencia los botones se pueden repetir
            b.Pressed += OnPressed;
        }
    }

    void OnDestroy()
    {
        if (buttons == null) return;
        foreach (var b in buttons) if (b != null) b.Pressed -= OnPressed;
    }

    void EnsureSequence()
    {
        int length = Mathf.Clamp(ScaleInt(sequenceLength.x, sequenceLength.y), 1, 32);
        if (sequence.Count == length) return;

        sequence.Clear();
        for (int i = 0; i < length; i++) sequence.Add(Random.Range(0, buttons.Length));
    }

    protected override void OnBegin()
    {
        if (!HasButtons) return;
        EnsureSequence();
        round = progressive ? 1 : sequence.Count;
        StartCoroutine(ShowRoutine(startDelay));
    }

    protected override void OnResetPuzzle()
    {
        if (buttons == null) return;
        foreach (var b in buttons) if (b != null) b.ResetButton();
        showing = false;
        inputIndex = 0;
    }

    protected override void ShowSolved()
    {
        if (buttons == null) return;
        foreach (var b in buttons)
        {
            if (b == null) continue;
            b.SetPressed(true);
            b.SetInteractable(false);
        }
    }

    IEnumerator ShowRoutine(float delay)
    {
        showing = true;
        SetAllInteractable(false);
        yield return new WaitForSeconds(delay);

        for (int i = 0; i < round; i++)
        {
            PuzzleButton b = buttons[sequence[i]];
            b.Light(true);
            yield return new WaitForSeconds(showStepTime);
            b.Light(false);
            yield return new WaitForSeconds(stepGap);
        }

        inputIndex = 0;
        showing = false;
        SetAllInteractable(true);
    }

    void SetAllInteractable(bool value)
    {
        if (buttons == null) return;
        foreach (var b in buttons) if (b != null) b.SetInteractable(value);
    }

    void OnPressed(PuzzleButton pressed)
    {
        if (!IsRunning || showing || !HasButtons) return;

        int index = System.Array.IndexOf(buttons, pressed);
        if (index == sequence[inputIndex])
        {
            inputIndex++;
            if (inputIndex >= round)
            {
                if (round >= sequence.Count)
                {
                    Solve();
                }
                else
                {
                    round++;
                    StartCoroutine(ShowRoutine(0.8f));
                }
            }
        }
        else
        {
            StartCoroutine(WrongRoutine());
        }
    }

    IEnumerator WrongRoutine()
    {
        showing = true;
        SetAllInteractable(false);
        if (buttons != null) foreach (var b in buttons) if (b != null) b.FlashWrong(0.4f);
        yield return new WaitForSeconds(replayDelay);
        yield return ShowRoutine(0.2f);
    }
}
