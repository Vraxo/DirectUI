﻿// Input/InputManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

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
}