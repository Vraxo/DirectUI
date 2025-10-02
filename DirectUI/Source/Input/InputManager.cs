using System.Numerics;
using System.Runtime.InteropServices; // Added for Marshal
using Raylib_cs; // Added for Raylib backend
using SDL3;
using static SDL3.SDL; // Added for SDL3 backend

namespace DirectUI.Input;

public class InputManager
{
    // Persistent state (held across frames)
    private Vector2 _currentMousePos = new(-1, -1);
    private Vector2 _previousMousePos = new(-1, -1);
    private bool _isLeftMouseButtonDown;
    private bool _isRightMouseButtonDown;
    private bool _isMiddleMouseButtonDown;
    private readonly HashSet<Keys> _heldKeys = new();

    // Per-frame state (reset every frame)
    private bool _wasLeftMouseClickedThisFrame;
    private bool _wasRightMouseClickedThisFrame;
    private bool _wasMiddleMouseClickedThisFrame;
    private float _scrollDeltaThisFrame;
    private readonly Queue<char> _typedCharsThisFrame = new();
    private readonly List<Keys> _pressedKeysThisFrame = new();
    private readonly List<Keys> _releasedKeysThisFrame = new();
    private readonly List<MouseButton> _pressedMouseButtonsThisFrame = new();

    public InputState GetCurrentState()
    {
        return new(
            _currentMousePos,
            _previousMousePos,
            _wasLeftMouseClickedThisFrame,
            _isLeftMouseButtonDown,
            _wasRightMouseClickedThisFrame,
            _isRightMouseButtonDown,
            _wasMiddleMouseClickedThisFrame,
            _isMiddleMouseButtonDown,
            _scrollDeltaThisFrame,
            [.. _typedCharsThisFrame], // Create a copy for the readonly list
            _pressedKeysThisFrame,
            _releasedKeysThisFrame,
            _heldKeys,
            [.. _pressedMouseButtonsThisFrame] // Create a copy for the readonly list
        );
    }

    public void PrepareNextFrame()
    {
        _previousMousePos = _currentMousePos;
        _wasLeftMouseClickedThisFrame = false;
        _wasRightMouseClickedThisFrame = false;
        _wasMiddleMouseClickedThisFrame = false;
        _scrollDeltaThisFrame = 0f;
        _typedCharsThisFrame.Clear();
        _pressedKeysThisFrame.Clear();
        _releasedKeysThisFrame.Clear();
        _pressedMouseButtonsThisFrame.Clear();
    }

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
        else if (button == MouseButton.Middle)
        {
            _isMiddleMouseButtonDown = true;
            _wasMiddleMouseClickedThisFrame = true;
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
        else if (button == MouseButton.Middle)
        {
            _isMiddleMouseButtonDown = false;
        }
    }

    public void AddCharacterInput(string text)
    {
        // Instead of breaking into individual chars, store the complete string
        // or at least mark surrogate pairs
        foreach (char c in text)
        {
            _typedCharsThisFrame.Enqueue(c);
        }

        // If it was a surrogate pair, add a marker
        if (text.Length == 2 && char.IsSurrogatePair(text[0], text[1]))
        {
            // We could add a special marker, but let's try a different approach
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

    public void HardReset()
    {
        // Resets persistent state that might be invalid after losing focus,
        // e.g. after a modal window closes.
        _isLeftMouseButtonDown = false;
        _isRightMouseButtonDown = false;
        _isMiddleMouseButtonDown = false;
        _heldKeys.Clear();
        // Also clear per-frame state for good measure.
        PrepareNextFrame();
    }

    public void ProcessVeldridInput(Veldrid.InputSnapshot snapshot)
    {
        SetMousePosition((int)snapshot.MousePosition.X, (int)snapshot.MousePosition.Y);
        AddMouseWheelDelta(snapshot.WheelDelta);

        foreach (Veldrid.KeyEvent keyEvent in snapshot.KeyEvents)
        {
            Keys mappedKey = MapVeldridKeyToDirectUIKey(keyEvent.Key);
            if (mappedKey == Keys.Unknown) continue;

            if (keyEvent.Down)
            {
                AddKeyPressed(mappedKey);
            }
            else
            {
                AddKeyReleased(mappedKey);
            }
        }

        foreach (var mouseEvent in snapshot.MouseEvents)
        {
            MouseButton mappedButton = MapVeldridMouseButtonToDirectUIButton(mouseEvent.MouseButton);
            if (mouseEvent.Down)
            {
                SetMouseDown(mappedButton);
            }
            else
            {
                SetMouseUp(mappedButton);
            }
        }

        foreach (char c in snapshot.KeyCharPresses)
        {
            AddCharacterInput(c);
        }
    }

    private Keys MapVeldridKeyToDirectUIKey(Veldrid.Key key)
    {
        throw new NotImplementedException();
    }

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
    public unsafe void ProcessSDL3Event(Event sdlEvent)
    {
        switch ((EventType)sdlEvent.Type)
        {
            case EventType.MouseButtonDown:
                SetMousePosition((int)sdlEvent.Button.X, (int)sdlEvent.Button.Y); // Update position on click
                SetMouseDown(MapSDL3MouseButtonToDirectUIButton(sdlEvent.Button.Button)); // Pass byte directly
                break;
            case EventType.MouseButtonUp:
                SetMousePosition((int)sdlEvent.Button.X, (int)sdlEvent.Button.Y); // Update position on release
                SetMouseUp(MapSDL3MouseButtonToDirectUIButton(sdlEvent.Button.Button)); // Pass byte directly
                break;
            case EventType.MouseMotion: // ADDED: Handle mouse motion to update position
                SetMousePosition((int)sdlEvent.Motion.X, (int)sdlEvent.Motion.Y);
                break;
            case EventType.MouseWheel:
                // SDL wheel delta is usually in integers (e.g., 1 or -1)
                // Normalize it similar to Win32/Veldrid
                float deltaY = sdlEvent.Wheel.Y;
                AddMouseWheelDelta(deltaY);
                break;
            case EventType.KeyDown:
                Keys mappedKeyDown = MapSDL3ScanCodeToDirectUIKey(sdlEvent.Key.Scancode);
                if (mappedKeyDown != Keys.Unknown) AddKeyPressed(mappedKeyDown);
                break;
            case EventType.KeyUp:
                Keys mappedKeyUp = MapSDL3ScanCodeToDirectUIKey(sdlEvent.Key.Scancode);
                if (mappedKeyUp != Keys.Unknown) AddKeyReleased(mappedKeyUp);
                break;
            case EventType.TextInput:
                // Correct way to get character input from SDL3 when `text` is exposed as `nint`
                // `sdlEvent.Text.text` is already an unmanaged pointer (`nint`).
                string typedText = Marshal.PtrToStringUTF8(sdlEvent.Text.Text);
                if (!string.IsNullOrEmpty(typedText))
                {
                    foreach (char c in typedText)
                    {
                        AddCharacterInput(c);
                    }
                }
                break;
        }
    }

    private static MouseButton MapVeldridMouseButtonToDirectUIButton(Veldrid.MouseButton vdButton)
    {
        return vdButton switch
        {
            Veldrid.MouseButton.Left => MouseButton.Left,
            Veldrid.MouseButton.Right => MouseButton.Right,
            Veldrid.MouseButton.Middle => MouseButton.Middle,
            Veldrid.MouseButton.Button1 => MouseButton.XButton1, // Assuming Button1 is XButton1
            Veldrid.MouseButton.Button2 => MouseButton.XButton2, // Assuming Button2 is XButton2
            _ => MouseButton.Left, // Default case
        };
    }

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

    private static MouseButton MapSDL3MouseButtonToDirectUIButton(byte sdlButton) // Changed parameter type to byte
    {
        return sdlButton switch
        {
            SDL.ButtonLeft => MouseButton.Left,
            SDL.ButtonMiddle => MouseButton.Middle,
            SDL.ButtonRight => MouseButton.Right,
            SDL.ButtonX1 => MouseButton.XButton1,
            SDL.ButtonX2 => MouseButton.XButton2,
            _ => MouseButton.Left, // Fallback, though ideally all used buttons are mapped.
        };
    }
    private static Keys MapSDL3ScanCodeToDirectUIKey(Scancode sdlScancode)
    {
        return sdlScancode switch
        {
            Scancode.Backspace => Keys.Backspace,
            Scancode.Tab => Keys.Tab,
            Scancode.Return => Keys.Enter, // RETURN is Enter
            Scancode.LShift => Keys.Shift,
            Scancode.RShift => Keys.Shift,
            Scancode.LCtrl => Keys.Control,
            Scancode.RCtrl => Keys.Control,
            Scancode.LAlt => Keys.Alt,
            Scancode.RAlt => Keys.Alt,
            Scancode.Pause => Keys.Pause,
            Scancode.Capslock => Keys.CapsLock,
            Scancode.Escape => Keys.Escape,
            Scancode.Space => Keys.Space,
            Scancode.Pageup => Keys.PageUp,
            Scancode.Pagedown => Keys.PageDown,
            Scancode.End => Keys.End,
            Scancode.Home => Keys.Home,
            Scancode.Left => Keys.LeftArrow,
            Scancode.Up => Keys.UpArrow,
            Scancode.Right => Keys.RightArrow,
            Scancode.Down => Keys.DownArrow,
            Scancode.Insert => Keys.Insert,
            Scancode.Delete => Keys.Delete,
            //Scancode.Num0 => Keys.D0,
            //Scancode.Num1 => Keys.D1,
            //Scancode.Num2 => Keys.D2,
            //Scancode.Num3 => Keys.D3,
            //Scancode.Num4 => Keys.D4,
            //Scancode.Num5 => Keys.D5,
            //Scancode.Num6 => Keys.D6,
            //Scancode.Num7 => Keys.D7,
            //Scancode.Num8 => Keys.D8,
            //Scancode.Num9 => Keys.D9,
            Scancode.A => Keys.A,
            Scancode.B => Keys.B,
            Scancode.C => Keys.C,
            Scancode.D => Keys.D,
            Scancode.E => Keys.E,
            Scancode.F => Keys.F,
            Scancode.G => Keys.G,
            Scancode.H => Keys.H,
            Scancode.I => Keys.I,
            Scancode.J => Keys.J,
            Scancode.K => Keys.K,
            Scancode.L => Keys.L,
            Scancode.M => Keys.M,
            Scancode.N => Keys.N,
            Scancode.O => Keys.O,
            Scancode.P => Keys.P,
            Scancode.Q => Keys.Q,
            Scancode.R => Keys.R,
            Scancode.S => Keys.S,
            Scancode.T => Keys.T,
            Scancode.U => Keys.U,
            Scancode.V => Keys.V,
            Scancode.W => Keys.W,
            Scancode.X => Keys.X,
            Scancode.Y => Keys.Y,
            Scancode.Z => Keys.Z,
            Scancode.F1 => Keys.F1,
            Scancode.F2 => Keys.F2,
            Scancode.F3 => Keys.F3,
            Scancode.F4 => Keys.F4,
            Scancode.F5 => Keys.F5,
            Scancode.F6 => Keys.F6,
            Scancode.F7 => Keys.F7,
            Scancode.F8 => Keys.F8,
            Scancode.F9 => Keys.F9,
            Scancode.F10 => Keys.F10,
            Scancode.F11 => Keys.F11,
            Scancode.F12 => Keys.F12,
            Scancode.LGUI => Keys.LeftWindows, // Left GUI key (Windows, Command, etc.)
            Scancode.RGUI => Keys.RightWindows, // Right GUI key
            Scancode.Application => Keys.Menu, // Application key (right-click menu)
            _ => Keys.Unknown,
        };
    }
}