namespace Cherris;

public sealed class DisplayServer
{
    private static DisplayServer? _instance;
    public static DisplayServer Instance => _instance ??= new();

    public DisplayServer()
    {
    }
}