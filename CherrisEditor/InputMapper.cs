// Entire file content here
using CherrisKey = Cherris.Key;
using DuiKey = DirectUI.Keys;
using CherrisMouseButton = Cherris.MouseButton;
using DuiMouseButton = DirectUI.MouseButton;

namespace CherrisEditor
{
    public static class InputMapper
    {
        public static CherrisKey MapDirectUIKey(DuiKey key)
        {
            return key switch
            {
                DuiKey.A => CherrisKey.A,
                DuiKey.B => CherrisKey.B,
                DuiKey.C => CherrisKey.C,
                DuiKey.D => CherrisKey.D,
                DuiKey.E => CherrisKey.E,
                DuiKey.F => CherrisKey.F,
                DuiKey.G => CherrisKey.G,
                DuiKey.H => CherrisKey.H,
                DuiKey.I => CherrisKey.I,
                DuiKey.J => CherrisKey.J,
                DuiKey.K => CherrisKey.K,
                DuiKey.L => CherrisKey.L,
                DuiKey.M => CherrisKey.M,
                DuiKey.N => CherrisKey.N,
                DuiKey.O => CherrisKey.O,
                DuiKey.P => CherrisKey.P,
                DuiKey.Q => CherrisKey.Q,
                DuiKey.R => CherrisKey.R,
                DuiKey.S => CherrisKey.S,
                DuiKey.T => CherrisKey.T,
                DuiKey.U => CherrisKey.U,
                DuiKey.V => CherrisKey.V,
                DuiKey.W => CherrisKey.W,
                DuiKey.X => CherrisKey.X,
                DuiKey.Y => CherrisKey.Y,
                DuiKey.Z => CherrisKey.Z,
                DuiKey.D0 => CherrisKey.Number0,
                DuiKey.D1 => CherrisKey.Number1,
                DuiKey.D2 => CherrisKey.Number2,
                DuiKey.D3 => CherrisKey.Number3,
                DuiKey.D4 => CherrisKey.Number4,
                DuiKey.D5 => CherrisKey.Number5,
                DuiKey.D6 => CherrisKey.Number6,
                DuiKey.D7 => CherrisKey.Number7,
                DuiKey.D8 => CherrisKey.Number8,
                DuiKey.D9 => CherrisKey.Number9,
                DuiKey.F1 => CherrisKey.F1,
                DuiKey.F2 => CherrisKey.F2,
                DuiKey.F3 => CherrisKey.F3,
                DuiKey.F4 => CherrisKey.F4,
                DuiKey.F5 => CherrisKey.F5,
                DuiKey.F6 => CherrisKey.F6,
                DuiKey.F7 => CherrisKey.F7,
                DuiKey.F8 => CherrisKey.F8,
                DuiKey.F9 => CherrisKey.F9,
                DuiKey.F10 => CherrisKey.F10,
                DuiKey.F11 => CherrisKey.F11,
                DuiKey.F12 => CherrisKey.F12,
                DuiKey.UpArrow => CherrisKey.Up,
                DuiKey.DownArrow => CherrisKey.Down,
                DuiKey.LeftArrow => CherrisKey.Left,
                DuiKey.RightArrow => CherrisKey.Right,
                DuiKey.Enter => CherrisKey.Enter,
                DuiKey.Escape => CherrisKey.Escape,
                DuiKey.Space => CherrisKey.Space,
                DuiKey.Tab => CherrisKey.Tab,
                DuiKey.Backspace => CherrisKey.BackSpace,
                DuiKey.Insert => CherrisKey.Insert,
                DuiKey.Delete => CherrisKey.Delete,
                DuiKey.PageUp => CherrisKey.PageUp,
                DuiKey.PageDown => CherrisKey.PageDown,
                DuiKey.Home => CherrisKey.Home,
                DuiKey.End => CherrisKey.End,
                DuiKey.CapsLock => CherrisKey.CapsLock,
                DuiKey.Shift => CherrisKey.ShiftLeft, // Map generic Shift to Left Shift
                DuiKey.Control => CherrisKey.ControlLeft, // Map generic Ctrl to Left Ctrl
                DuiKey.Alt => CherrisKey.AltLeft, // Map generic Alt to Left Alt
                _ => CherrisKey.Unknown,
            };
        }

        public static CherrisMouseButton MapDirectUIMouseButton(DuiMouseButton button)
        {
            return button switch
            {
                DuiMouseButton.Left => CherrisMouseButton.Left,
                DuiMouseButton.Middle => CherrisMouseButton.Middle,
                DuiMouseButton.Right => CherrisMouseButton.Right,
                // DirectUI doesn't have names for XButtons that match Cherris exactly
                // We'll map them if they are added in the future.
                _ => CherrisMouseButton.LastButton,
            };
        }
    }
}