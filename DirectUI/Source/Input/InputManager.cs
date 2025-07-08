// Input/InputManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Raylib_cs; // Added for Raylib backend

namespace DirectUI.Input;

/// <summary>
/// Aggregates raw input events over a frame and provides a snapshot of the input state.
/// </summary>
public class InputManager
{
    // Persistent state (held across frames)
    private Vector2 _currentMousePos = new(-1, -1);
    private bool _isLeftMouseButtonDown;
    private bool _isRightMouseButtonDown;
    private readonly HashSet<Keys> _heldKeys = new();

    // Per-frame state (reset every frame)
    private bool _wasLeftMouseClickedThisFrame;
    private bool _wasRightMouseClickedThisFrame;
    private float _scrollDeltaThisFrame;
    private readonly Queue<char> _typedCharsThisFrame = new();
    private readonly List<Keys> _pressedKeysThisFrame = new();
    private readonly List<Keys> _releasedKeysThisFrame = new();
    private readonly List<MouseButton> _pressedMouseButtonsThisFrame = new();

    /// <summary>
    /// Creates a snapshot of the current input state for the UI to process.
    /// </summary>
    public InputState GetCurrentState()
    {
        return new InputState(
            _currentMousePos,
            _wasLeftMouseClickedThisFrame,
            _isLeftMouseButtonDown,
            _wasRightMouseClickedThisFrame,
            _isRightMouseButtonDown,
            _scrollDeltaThisFrame,
            _typedCharsThisFrame.ToList(), // Create a copy for the readonly list
            _pressedKeysThisFrame,
            _releasedKeysThisFrame,
            _heldKeys,
            _pressedMouseButtonsThisFrame.ToList() // Create a copy for the readonly list
        );
    }

    /// <summary>
    /// Resets the per-frame input state. Should be called after a frame has been rendered.
    /// </summary>
    public void PrepareNextFrame()
    {
        _wasLeftMouseClickedThisFrame = false;
        _wasRightMouseClickedThisFrame = false;
        _scrollDeltaThisFrame = 0f;
        _typedCharsThisFrame.Clear();
        _pressedKeysThisFrame.Clear();
        _releasedKeysThisFrame.Clear();
        _pressedMouseButtonsThisFrame.Clear();
    }

    // --- Raw Event Handlers ---

    public void SetMousePosition(int x, int y)
    {
        _currentMousePos = new Vector2(x, y);
    }

    public void AddMouseWheelDelta(float delta)
    {
        _scrollDeltaThisFrame += delta;
    }

    public void SetMouseDown(MouseButton button)
    {
        _pressedMouseButtonsThisFrame.Add(button);

        if (button == MouseButton.Left)
        {
            _isLeftMouseButtonDown = true;
            _wasLeftMouseClickedThisFrame = true;
        }
        else if (button == MouseButton.Right)
        {
            _isRightMouseButtonDown = true;
            _wasRightMouseClickedThisFrame = true;
        }
    }

    public void SetMouseUp(MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            _isLeftMouseButtonDown = false;
        }
        else if (button == MouseButton.Right)
        {
            _isRightMouseButtonDown = false;
        }
    }

    public void AddCharacterInput(char c)
    {
        // Filter out control characters except for tab and newline which might be useful in text boxes.
        if (!char.IsControl(c) || c == '\t' || c == '\n')
        {
            _typedCharsThisFrame.Enqueue(c);
        }
    }

    public void AddKeyPressed(Keys key)
    {
        if (!_heldKeys.Contains(key))
        {
            _pressedKeysThisFrame.Add(key);
            _heldKeys.Add(key);
        }
    }

    public void AddKeyReleased(Keys key)
    {
        if (_heldKeys.Contains(key))
        {
            _releasedKeysThisFrame.Add(key);
            _heldKeys.Remove(key);
        }
    }

    /// <summary>
    /// Processes Raylib-specific input events and updates the internal state.
    /// This method should be called once per frame when using the Raylib backend.
    /// </summary>
    public void ProcessRaylibInput()
    {
        // Mouse position
        Vector2 mousePos = Raylib.GetMousePosition();
        SetMousePosition((int)mousePos.X, (int)mousePos.Y);

        // Mouse buttons
        if (Raylib.IsMouseButtonPressed(Raylib_cs.MouseButton.Left)) SetMouseDown(MouseButton.Left);
        if (Raylib.IsMouseButtonReleased(Raylib_cs.MouseButton.Left)) SetMouseUp(MouseButton.Left);
        if (Raylib.IsMouseButtonPressed(Raylib_cs.MouseButton.Right)) SetMouseDown(MouseButton.Right);
        if (Raylib.IsMouseButtonReleased(Raylib_cs.MouseButton.Right)) SetMouseUp(MouseButton.Right);
        if (Raylib.IsMouseButtonPressed(Raylib_cs.MouseButton.Middle)) SetMouseDown(MouseButton.Middle);
        if (Raylib.IsMouseButtonReleased(Raylib_cs.MouseButton.Middle)) SetMouseUp(MouseButton.Middle);

        // Mouse wheel
        float wheelMove = Raylib.GetMouseWheelMove();
        if (wheelMove != 0) AddMouseWheelDelta(wheelMove);

        // Keyboard keys
        foreach (KeyboardKey rlKey in Enum.GetValues(typeof(KeyboardKey)))
        {
            if (rlKey == KeyboardKey.Null) continue; // Skip NULL key

            Keys mappedKey = MapRaylibKeyToDirectUIKey(rlKey);
            if (mappedKey == Keys.Unknown) continue;

            if (Raylib.IsKeyPressed(rlKey)) AddKeyPressed(mappedKey);
            if (Raylib.IsKeyReleased(rlKey)) AddKeyReleased(mappedKey);
        }

        // Character input
        int charValue = Raylib.GetCharPressed();
        while (charValue > 0)
        {
            AddCharacterInput((char)charValue);
            charValue = Raylib.GetCharPressed();
        }
    }

    /// <summary>
    /// Maps a Raylib KeyboardKey enum to a DirectUI Keys enum.
    /// This is an internal utility for the Raylib backend.
    /// </summary>
    private static Keys MapRaylibKeyToDirectUIKey(KeyboardKey rlKey)
    {
        // Updated to use PascalCase enum members without the KEY_ prefix
        return rlKey switch
        {
            KeyboardKey.Backspace => Keys.Backspace,
            KeyboardKey.Tab => Keys.Tab,
            KeyboardKey.Enter => Keys.Enter,
            KeyboardKey.LeftShift => Keys.Shift,
            KeyboardKey.RightShift => Keys.Shift,
            KeyboardKey.LeftControl => Keys.Control,
            KeyboardKey.RightControl => Keys.Control,
            KeyboardKey.LeftAlt => Keys.Alt,
            KeyboardKey.RightAlt => Keys.Alt,
            KeyboardKey.Pause => Keys.Pause,
            KeyboardKey.CapsLock => Keys.CapsLock,
            KeyboardKey.Escape => Keys.Escape,
            KeyboardKey.Space => Keys.Space,
            KeyboardKey.PageUp => Keys.PageUp,
            KeyboardKey.PageDown => Keys.PageDown,
            KeyboardKey.End => Keys.End,
            KeyboardKey.Home => Keys.Home,
            KeyboardKey.Left => Keys.LeftArrow,
            KeyboardKey.Up => Keys.UpArrow,
            KeyboardKey.Right => Keys.RightArrow,
            KeyboardKey.Down => Keys.DownArrow,
            KeyboardKey.Insert => Keys.Insert,
            KeyboardKey.Delete => Keys.Delete,
            KeyboardKey.Zero => Keys.D0,
            KeyboardKey.One => Keys.D1,
            KeyboardKey.Two => Keys.D2,
            KeyboardKey.Three => Keys.D3,
            KeyboardKey.Four => Keys.D4,
            KeyboardKey.Five => Keys.D5,
            KeyboardKey.Six => Keys.D6,
            KeyboardKey.Seven => Keys.D7,
            KeyboardKey.Eight => Keys.D8,
            KeyboardKey.Nine => Keys.D9,
            KeyboardKey.A => Keys.A,
            KeyboardKey.B => Keys.B,
            KeyboardKey.C => Keys.C,
            KeyboardKey.D => Keys.D,
            KeyboardKey.E => Keys.E,
            KeyboardKey.F => Keys.F,
            KeyboardKey.G => Keys.G,
            KeyboardKey.H => Keys.H,
            KeyboardKey.I => Keys.I,
            KeyboardKey.J => Keys.J,
            KeyboardKey.K => Keys.K,
            KeyboardKey.L => Keys.L,
            KeyboardKey.M => Keys.M,
            KeyboardKey.N => Keys.N,
            KeyboardKey.O => Keys.O,
            KeyboardKey.P => Keys.P,
            KeyboardKey.Q => Keys.Q,
            KeyboardKey.R => Keys.R,
            KeyboardKey.S => Keys.S,
            KeyboardKey.T => Keys.T,
            KeyboardKey.U => Keys.U,
            KeyboardKey.V => Keys.V,
            KeyboardKey.W => Keys.W,
            KeyboardKey.X => Keys.X,
            KeyboardKey.Y => Keys.Y,
            KeyboardKey.Z => Keys.Z,
            KeyboardKey.F1 => Keys.F1,
            KeyboardKey.F2 => Keys.F2,
            KeyboardKey.F3 => Keys.F3,
            KeyboardKey.F4 => Keys.F4,
            KeyboardKey.F5 => Keys.F5,
            KeyboardKey.F6 => Keys.F6,
            KeyboardKey.F7 => Keys.F7,
            KeyboardKey.F8 => Keys.F8,
            KeyboardKey.F9 => Keys.F9,
            KeyboardKey.F10 => Keys.F10,
            KeyboardKey.F11 => Keys.F11,
            KeyboardKey.F12 => Keys.F12,
            _ => Keys.Unknown,
        };
    }
}