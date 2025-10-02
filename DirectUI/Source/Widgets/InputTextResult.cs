namespace DirectUI;

public readonly struct InputTextResult
{
    public bool ValueChanged { get; }
    public bool EnterPressed { get; }

    public InputTextResult(bool valueChanged, bool enterPressed)
    {
        ValueChanged = valueChanged;
        EnterPressed = enterPressed;
    }

    public static implicit operator bool(InputTextResult result) => result.ValueChanged;
}