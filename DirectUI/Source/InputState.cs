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
    public readonly IReadOnlyList<Keys> PressedKeys; // Keys pressed down this frame
    public readonly IReadOnlyList<Keys> ReleasedKeys; // Keys released this frame
    public readonly IReadOnlyCollection<Keys> HeldKeys; // Keys currently held down

    public InputState(
        Vector2 mousePosition,
        bool wasLeftMousePressedThisFrame,
        bool isLeftMouseDown,
        IReadOnlyList<char> typedCharacters,
        IReadOnlyList<Keys> pressedKeys,
        IReadOnlyList<Keys> releasedKeys,
        IReadOnlyCollection<Keys> heldKeys)
    {
        MousePosition = mousePosition;
        WasLeftMousePressedThisFrame = wasLeftMousePressedThisFrame;
        IsLeftMouseDown = isLeftMouseDown;
        TypedCharacters = typedCharacters;
        PressedKeys = pressedKeys;
        ReleasedKeys = releasedKeys;
        HeldKeys = heldKeys;
    }
}