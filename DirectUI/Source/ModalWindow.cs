using System;
using Vortice.Mathematics;

namespace DirectUI;

/// <summary>
/// A specialized window that operates modally over an owner window.
/// </summary>
public class ModalWindow : Direct2DAppWindow
{
    private readonly Win32Window _owner;
    private readonly Action<UIContext> _drawCallback;

    public ModalWindow(Win32Window owner, string title, int width, int height, Action<UIContext> drawCallback)
        : base(title, width, height)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _drawCallback = drawCallback ?? throw new ArgumentNullException(nameof(drawCallback));
    }

    /// <summary>
    /// Overrides AppHost creation to inject the specific drawing logic for this modal window.
    /// </summary>
    protected override AppHost CreateAppHost()
    {
        // A slightly different background for modals
        var backgroundColor = new Color4(37 / 255f, 37 / 255f, 38 / 255f, 1.0f);
        return new AppHost(_drawCallback, backgroundColor);
    }

    /// <summary>
    /// Creates the window with modal-specific styles and disables its owner.
    /// </summary>
    public bool CreateAsModal()
    {
        if (Handle != IntPtr.Zero) return true;

        uint style = NativeMethods.WS_POPUP | NativeMethods.WS_CAPTION | NativeMethods.WS_SYSMENU | NativeMethods.WS_VISIBLE | NativeMethods.WS_THICKFRAME;

        if (!base.Create(_owner.Handle, style))
        {
            return false;
        }

        if (_owner.Handle != IntPtr.Zero)
        {
            NativeMethods.EnableWindow(_owner.Handle, false);
        }
        return true;
    }

    /// <summary>
    /// Re-enables the owner window when this modal window is destroyed.
    /// </summary>
    protected override void OnDestroy()
    {
        if (_owner.Handle != IntPtr.Zero)
        {
            NativeMethods.EnableWindow(_owner.Handle, true);
        }
        base.OnDestroy();
    }
}