namespace Cocoshell.Input;

public enum BindingType
{
    Keyboard,
    MouseButton
}

public class InputBinding
{
    // Property names match the YAML file keys (PascalCase)
    public BindingType Type { get; set; }
    public string KeyOrButton { get; set; } = string.Empty;
}