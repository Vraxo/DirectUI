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

    // Action Keys
    /// <summary>The BACKSPACE key.</summary>
    Backspace = 0x08,
    /// <summary>The TAB key.</summary>
    Tab = 0x09,
    /// <summary>The ENTER key.</summary>
    Enter = 0x0D,
    /// <summary>The SHIFT key.</summary>
    Shift = 0x10,
    /// <summary>The CTRL key.</summary>
    Control = 0x11,
    /// <summary>The ALT key.</summary>
    Alt = 0x12,
    /// <summary>The PAUSE key.</summary>
    Pause = 0x13,
    /// <summary>The CAPS LOCK key.</summary>
    CapsLock = 0x14,
    /// <summary>The ESC key.</summary>
    Escape = 0x1B,
    /// <summary>The SPACEBAR key.</summary>
    Space = 0x20,
    /// <summary>The PAGE UP key.</summary>
    PageUp = 0x21,
    /// <summary>The PAGE DOWN key.</summary>
    PageDown = 0x22,
    /// <summary>The END key.</summary>
    End = 0x23,
    /// <summary>The HOME key.</summary>
    Home = 0x24,
    /// <summary>The LEFT ARROW key.</summary>
    LeftArrow = 0x25,
    /// <summary>The UP ARROW key.</summary>
    UpArrow = 0x26,
    /// <summary>The RIGHT ARROW key.</summary>
    RightArrow = 0x27,
    /// <summary>The DOWN ARROW key.</summary>
    DownArrow = 0x28,
    /// <summary>The INSERT key.</summary>
    Insert = 0x2D,
    /// <summary>The DELETE key.</summary>
    Delete = 0x2E,

    // Digit Keys
    /// <summary>The 0 key.</summary>
    D0 = 0x30,
    /// <summary>The 1 key.</summary>
    D1 = 0x31,
    /// <summary>The 2 key.</summary>
    D2 = 0x32,
    /// <summary>The 3 key.</summary>
    D3 = 0x33,
    /// <summary>The 4 key.</summary>
    D4 = 0x34,
    /// <summary>The 5 key.</summary>
    D5 = 0x35,
    /// <summary>The 6 key.</summary>
    D6 = 0x36,
    /// <summary>The 7 key.</summary>
    D7 = 0x37,
    /// <summary>The 8 key.</summary>
    D8 = 0x38,
    /// <summary>The 9 key.</summary>
    D9 = 0x39,

    // Letter Keys
    /// <summary>The A key.</summary>
    A = 0x41,
    /// <summary>The B key.</summary>
    B = 0x42,
    /// <summary>The C key.</summary>
    C = 0x43,
    /// <summary>The D key.</summary>
    D = 0x44,
    /// <summary>The E key.</summary>
    E = 0x45,
    /// <summary>The F key.</summary>
    F = 0x46,
    /// <summary>The G key.</summary>
    G = 0x47,
    /// <summary>The H key.</summary>
    H = 0x48,
    /// <summary>The I key.</summary>
    I = 0x49,
    /// <summary>The J key.</summary>
    J = 0x4A,
    /// <summary>The K key.</summary>
    K = 0x4B,
    /// <summary>The L key.</summary>
    L = 0x4C,
    /// <summary>The M key.</summary>
    M = 0x4D,
    /// <summary>The N key.</summary>
    N = 0x4E,
    /// <summary>The O key.</summary>
    O = 0x4F,
    /// <summary>The P key.</summary>
    P = 0x50,
    /// <summary>The Q key.</summary>
    Q = 0x51,
    /// <summary>The R key.</summary>
    R = 0x52,
    /// <summary>The S key.</summary>
    S = 0x53,
    /// <summary>The T key.</summary>
    T = 0x54,
    /// <summary>The U key.</summary>
    U = 0x55,
    /// <summary>The V key.</summary>
    V = 0x56,
    /// <summary>The W key.</summary>
    W = 0x57,
    /// <summary>The X key.</summary>
    X = 0x58,
    /// <summary>The Y key.</summary>
    Y = 0x59,
    /// <summary>The Z key.</summary>
    Z = 0x5A,

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
}