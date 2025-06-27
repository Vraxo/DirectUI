// InputState.cs
using System.Collections.Generic;
using System.Numerics;

namespace DirectUI;

// Holds input state needed for UI processing in a single frame
public readonly struct InputState
{
    public readonly Vector2 MousePosition;
    public readonly bool WasLeftMousePressedThisFrame; // True if the left button went down this frame
    public readonly bool IsLeftMouseDown; // True if the left button is currently held down
    public readonly IReadOnlyList<char> TypedCharacters; // Characters typed this frame

    public InputState(Vector2 mousePosition, bool wasLeftMousePressedThisFrame, bool isLeftMouseDown, IReadOnlyList<char> typedCharacters)
    {
        MousePosition = mousePosition;
        WasLeftMousePressedThisFrame = wasLeftMousePressedThisFrame;
        IsLeftMouseDown = isLeftMouseDown;
        TypedCharacters = typedCharacters;
    }
}