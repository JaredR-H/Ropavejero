using TMPro;
using UnityEngine;

/// Botones con tiempo limite (como las monedas rojas de Mario): hay que tenerlos todos presionados a la vez.
/// - GlobalWindow: al presionar el primero empieza una cuenta de X segundos para presionar TODOS. Si se acaba, todos se reinician.
/// - PerButton: cada boton se levanta solo X segundos despues de presionarlo. Hay que tener todos hundidos al mismo tiempo.
public class TimedButtonsPuzzle : RoomPuzzle
{
    public enum TimerMode { GlobalWindow, PerButton }

    [SerializeField] PuzzleButton[] buttons;
    [SerializeField] TimerMode mode = TimerMode.GlobalWindow;
    [Tooltip("GlobalWindow: segundos para presionar todos (facil, dificil). Menos tiempo = mas dificil.")]
    [SerializeField] Vector2 windowSeconds = new Vector2(10f, 6f);
    [Tooltip("PerButton: segundos que dura hundido cada boton (facil, dificil).")]
    [SerializeField] Vector2 holdSeconds = new Vector2(8f, 4f);
    [Tooltip("Opcional: texto con los segundos que quedan.")]
    [SerializeField] TMP_Text countdownLabel;
    [Tooltip("Cuando quedan menos segundos que esto, los botones parpadean para avisar.")]
    [SerializeField] float blinkBelow = 2f;

    bool windowActive;
    float windowRemaining;
    float[] expiry;

    protected override void Awake()
    {
        base.Awake();
        if (buttons == null) return;
        expiry = new float[buttons.Length];
        foreach (var b in buttons) if (b != null) b.Pressed += OnPressed;
    }

    void OnDestroy()
    {
        if (buttons == null) return;
        foreach (var b in buttons) if (b != null) b.Pressed -= OnPressed;
    }

    protected override void OnBegin()
    {
        windowActive = false;
        SetLabel("");
    }

    protected override void OnResetPuzzle()
    {
        if (buttons == null) return;
        foreach (var b in buttons) if (b != null) b.ResetButton();
        windowActive = false;
        SetLabel("");
    }

    protected override void ShowSolved()
    {
        if (buttons == null) return;
        foreach (var b in buttons)
        {
            if (b == null) continue;
            b.Light(false);
            b.SetPressed(true);
            b.SetInteractable(false);
        }
        windowActive = false;
        SetLabel("");
    }

    void OnPressed(PuzzleButton pressed)
    {
        if (!IsRunning) return;

        if (mode == TimerMode.GlobalWindow)
        {
            if (!windowActive)
            {
                windowActive = true;
                windowRemaining = Scale(windowSeconds.x, windowSeconds.y);
            }
        }
        else
        {
            int index = System.Array.IndexOf(buttons, pressed);
            if (index >= 0) expiry[index] = Scale(holdSeconds.x, holdSeconds.y);
        }

        if (AllPressed()) Solve();
    }

    bool AllPressed()
    {
        foreach (var b in buttons)
            if (b != null && !b.IsPressed) return false;
        return true;
    }

    void Update()
    {
        if (!IsRunning || buttons == null) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        float dt = Time.deltaTime;

        if (mode == TimerMode.GlobalWindow)
        {
            if (!windowActive) return;

            windowRemaining -= dt;
            SetLabel(Mathf.CeilToInt(Mathf.Max(0f, windowRemaining)).ToString());
            BlinkPressed(windowRemaining);

            if (windowRemaining <= 0f) Fail();
        }
        else
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                if (b == null || !b.IsPressed) continue;

                expiry[i] -= dt;
                if (expiry[i] <= 0f)
                {
                    b.ResetButton();
                    b.FlashWrong(0.3f);
                }
                else if (expiry[i] < blinkBelow)
                {
                    b.Light(Mathf.Sin(Time.time * 18f) > 0f);
                }
            }
        }
    }

    // Los botones ya presionados parpadean cuando queda poco tiempo
    void BlinkPressed(float remaining)
    {
        if (remaining > blinkBelow) return;
        bool on = Mathf.Sin(Time.time * 18f) > 0f;
        foreach (var b in buttons) if (b != null && b.IsPressed) b.Light(on);
    }

    void Fail()
    {
        windowActive = false;
        SetLabel("");
        foreach (var b in buttons)
        {
            if (b == null) continue;
            b.ResetButton();
            b.FlashWrong(0.4f);
        }
    }

    void SetLabel(string text)
    {
        if (countdownLabel != null) countdownLabel.text = text;
    }
}
