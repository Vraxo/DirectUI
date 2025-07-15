namespace Cherris;

public class AudioPlayer : Node
{
    public string Audio { get; set; } = string.Empty;
    public bool AutoPlay { get; set; } = false;
    public string Bus { get; set; } = "Master";
}