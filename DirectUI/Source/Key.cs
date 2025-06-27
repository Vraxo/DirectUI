namespace DirectUI;

/// <summary>
/// Defines keyboard keys for use in application-level input handling.
/// The values correspond to Win32 Virtual-Key Codes.
/// </summary>
public enum Keys
{
    /// <summary>
    /// The key is not recognized.
    /// </summary>
    Unknown = 0,

    // Function Keys
    /// <summary>The F1 key.</summary>
    F1 = 0x70,
    /// <summary>The F2 key.</summary>
    F2 = 0x71,
    /// <summary>The F3 key.</summary>
    F3 = 0x72,
    /// <summary>The F4 key.</summary>
    F4 = 0x73,
    /// <summary>The F5 key.</summary>
    F5 = 0x74,
    /// <summary>The F6 key.</summary>
    F6 = 0x75,
    /// <summary>The F7 key.</summary>
    F7 = 0x76,
    /// <summary>The F8 key.</summary>
    F8 = 0x77,
    /// <summary>The F9 key.</summary>
    F9 = 0x78,
    /// <summary>The F10 key.</summary>
    F10 = 0x79,
    /// <summary>The F11 key.</summary>
    F11 = 0x7A,
    /// <summary>The F12 key.</summary>
    F12 = 0x7B,

    // Other Keys
    /// <summary>The ESC key.</summary>
    Escape = 0x1B,
    // Add more keys like Enter, Arrows, etc. if needed in the future.
}