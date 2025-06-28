using Vortice.Mathematics;

namespace DirectUI;

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

    protected override AppHost CreateAppHost()
    {
        Color4 backgroundColor = new(37 / 255f, 37 / 255f, 38 / 255f, 1.0f);
        return new(_drawCallback, backgroundColor);
    }

    public bool CreateAsModal()
    {
        if (Handle != IntPtr.Zero)
        {
            return true;
        }

        uint style =
            NativeMethods.WS_POPUP |
            NativeMethods.WS_CAPTION |
            NativeMethods.WS_SYSMENU |
            NativeMethods.WS_VISIBLE |
            NativeMethods.WS_THICKFRAME;

        int? x = null;
        int? y = null;

        if (Handle != IntPtr.Zero && GetWindowRect(out NativeMethods.RECT ownerRect))
        {
            int ownerWidth = ownerRect.right - ownerRect.left;
            int ownerHeight = ownerRect.bottom - ownerRect.top;
            int modalWidth = Width;
            int modalHeight = Height;

            x = ownerRect.left + (ownerWidth - modalWidth) / 2;
            y = ownerRect.top + (ownerHeight - modalHeight) / 2;
        }

        if (!Create(Handle, style, x, y))
        {
            return false;
        }

        if (Handle == IntPtr.Zero)
        {
            return true;
        }

        NativeMethods.EnableWindow(Handle, false);

        return true;
    }

    protected override void OnDestroy()
    {
        if (Handle != IntPtr.Zero)
        {
            NativeMethods.EnableWindow(Handle, true);
        }

        base.OnDestroy();
    }
}