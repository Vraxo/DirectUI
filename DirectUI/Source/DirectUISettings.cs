namespace DirectUI;

/// <summary>
/// A model for storing global DirectUI settings that are persisted to a file.
/// </summary>
public class DirectUISettings
{
    /// <summary>
    /// The global zoom level for the entire user interface.
    /// </summary>
    public float UIScale { get; set; } = 1.0f;
}