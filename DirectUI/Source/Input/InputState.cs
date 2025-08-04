// InputState.cs
using System.Collections.Generic;
using System.Numerics;

namespace DirectUI;

// Holds input state needed for UI processing in a single frame
public readonly struct InputState
{
    public readonly Vector2 MousePosition;
    public readonly Vector2 PreviousMousePosition;
    public readonly bool WasLeftMousePressedThisFrame; // True if the left button went down this frame
    public readonly bool IsLeftMouseDown; // True if the left button is currently held down
    public readonly bool WasRightMousePressedThisFrame; // True if the right button went down this frame
    public readonly bool IsRightMouseDown; // True if the right button is currently held down
    public readonly bool WasMiddleMousePressedThisFrame;
    public readonly bool IsMiddleMouseDown;
    public readonly float ScrollDelta; // Mouse wheel scroll amount this frame
    public readonly IReadOnlyList<char> TypedCharacters; // Characters typed this frame
    public readonly IReadOnlyList<Keys> PressedKeys; // Keys pressed down this frame
    public readonly IReadOnlyList<Keys> ReleasedKeys; // Keys released this frame
    public readonly IReadOnlyCollection<Keys> HeldKeys; // Keys currently held down
    public readonly IReadOnlyList<MouseButton> PressedMouseButtons; // Mouse buttons pressed down this frame

    public InputState(
        Vector2 mousePosition,
        Vector2 previousMousePosition,
        bool wasLeftMousePressedThisFrame,
        bool isLeftMouseDown,
        bool wasRightMousePressedThisFrame,
        bool isRightMouseDown,
        bool wasMiddleMousePressedThisFrame,
        bool isMiddleMouseDown,
        float scrollDelta,
        IReadOnlyList<char> typedCharacters,
        IReadOnlyList<Keys> pressedKeys,
        IReadOnlyList<Keys> releasedKeys,
        IReadOnlyCollection<Keys> heldKeys,
        IReadOnlyList<MouseButton> pressedMouseButtons)
    {
        MousePosition = mousePosition;
        PreviousMousePosition = previousMousePosition;
        WasLeftMousePressedThisFrame = wasLeftMousePressedThisFrame;
        IsLeftMouseDown = isLeftMouseDown;
        WasRightMousePressedThisFrame = wasRightMousePressedThisFrame;
        IsRightMouseDown = isRightMouseDown;
        WasMiddleMousePressedThisFrame = wasMiddleMousePressedThisFrame;
        IsMiddleMouseDown = isMiddleMouseDown;
        ScrollDelta = scrollDelta;
        TypedCharacters = typedCharacters;
        PressedKeys = pressedKeys;
        ReleasedKeys = releasedKeys;
        HeldKeys = heldKeys;
        PressedMouseButtons = pressedMouseButtons;
    }
}