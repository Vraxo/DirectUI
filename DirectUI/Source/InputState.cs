// InputState.cs
using System.Numerics;

namespace DirectUI;

// Holds input state needed for UI processing in a single frame
public readonly struct InputState
{
    public readonly Vector2 MousePosition;
    public readonly bool WasLeftMousePressedThisFrame; // True if the left button went down this frame
    public readonly bool IsLeftMouseDown; // True if the left button is currently held down
    // Add fields for Right mouse button if ClickBehavior.Right/Both is used

    public InputState(Vector2 mousePosition, bool wasLeftMousePressedThisFrame, bool isLeftMouseDown)
    {
        MousePosition = mousePosition;
        WasLeftMousePressedThisFrame = wasLeftMousePressedThisFrame;
        IsLeftMouseDown = isLeftMouseDown;
    }
}