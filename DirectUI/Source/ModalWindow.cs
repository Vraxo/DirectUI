using DirectUI.Core; // Added for IWindowHost, IModalWindowService
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;
using Vortice.DirectWrite; // For DirectWriteTextService
using Vortice.Direct2D1; // For Direct2DRenderer

namespace DirectUI;

public class ModalWindow : Win32Window
{
    private readonly Win32Window _owner;
    private readonly Action<UIContext> _drawCallback;
    private AppServices? _appServices; // Changed to the new bundle

    public ModalWindow(Win32Window owner, string title, int width, int height, Action<UIContext> drawCallback)
        : base(title, width, height)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _drawCallback = drawCallback ?? throw new ArgumentNullException(nameof(drawCallback));
    }

    protected override bool Initialize()
    {
        Console.WriteLine("ModalWindow initializing...");
        try
        {
            _appServices = Win32AppServicesInitializer.Initialize(Handle, GetClientRectSize(), _drawCallback, new Color4(37 / 255f, 37 / 255f, 38 / 255f, 1.0f));
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize modal window services: {ex.Message}");
            return false;
        }
    }

    protected override void Cleanup()
    {
        Console.WriteLine("ModalWindow cleaning up its resources...");
        _appServices?.AppEngine.Cleanup();
        _appServices?.TextService.Cleanup();
        (_appServices?.Renderer as DirectUI.Backends.Direct2DRenderer)?.Cleanup();
        _appServices?.GraphicsDevice.Cleanup();

        _appServices = null; // Clear the bundle
    }

    protected override void OnPaint()
    {
        if (_appServices is null || !_appServices.GraphicsDevice.IsInitialized)
        {
            Console.WriteLine("Modal window render services not initialized. Skipping paint.");
            return;
        }

        _appServices.GraphicsDevice.BeginDraw();
        try
        {
            // Clear the background before drawing anything else. This fixes smearing artifacts.
            if (_appServices.AppEngine is not null && _appServices.GraphicsDevice.RenderTarget is not null)
            {
                _appServices.GraphicsDevice.RenderTarget.Clear(_appServices.AppEngine.BackgroundColor);
            }

            // Pass the modal's specific renderer and text service to the AppEngine
            _appServices.AppEngine.UpdateAndRender(_appServices.Renderer, _appServices.TextService);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during modal window drawing: {ex}");
            _appServices.GraphicsDevice.Cleanup();
        }
        finally
        {
            _appServices.GraphicsDevice.EndDraw();
        }
    }

    public override void FrameUpdate()
    {
        Invalidate(); // Always invalidate to trigger a paint message for a continuous render loop.
    }

    protected override void OnSize(int width, int height)
    {
        _appServices?.GraphicsDevice.Resize(new SizeI(width, height));
    }

    // Input handlers for the modal window pass input to its own AppEngine's InputManager.
    protected override void OnMouseMove(int x, int y)
    {
        _appServices?.AppEngine.Input.SetMousePosition(x, y);
        Invalidate();
    }

    protected override void OnMouseDown(MouseButton button, int x, int y)
    {
        _appServices?.AppEngine.Input.SetMousePosition(x, y); // Update position on click
        _appServices?.AppEngine.Input.SetMouseDown(button);
        Invalidate();
    }

    protected override void OnMouseUp(MouseButton button, int x, int y)
    {
        _appServices?.AppEngine.Input.SetMousePosition(x, y); // Update position on release
        _appServices?.AppEngine.Input.SetMouseUp(button);
        Invalidate();
    }

    protected override void OnMouseWheel(float delta)
    {
        _appServices?.AppEngine.Input.AddMouseWheelDelta(delta);
        Invalidate();
    }

    protected override void OnKeyDown(Keys key)
    {
        _appServices?.AppEngine.Input.AddKeyPressed(key);

        if (key == Keys.Escape)
        {
            Close(); // Close on Escape
        }
        Invalidate();
    }

    protected override void OnKeyUp(Keys key)
    {
        _appServices?.AppEngine.Input.AddKeyReleased(key);
        Invalidate();
    }

    protected override void OnChar(char c)
    {
        _appServices?.AppEngine.Input.AddCharacterInput(c);
        Invalidate();
    }

    protected override bool OnClose() { return true; }

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

        if (_owner.Handle != IntPtr.Zero && _owner.GetWindowRect(out NativeMethods.RECT ownerRect))
        {
            int ownerWidth = ownerRect.right - ownerRect.left;
            int ownerHeight = ownerRect.bottom - ownerRect.top;
            int modalWidth = Width;
            int modalHeight = Height;

            x = ownerRect.left + (ownerWidth - modalWidth) / 2;
            y = ownerRect.top + (ownerHeight - modalHeight) / 2;
        }

        if (!Create(_owner.Handle, style, x, y))
        {
            return false;
        }

        if (Handle == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.EnableWindow(_owner.Handle, false);

        return true;
    }

    protected override void OnDestroy()
    {
        if (_owner.Handle != IntPtr.Zero)
        {
            NativeMethods.EnableWindow(_owner.Handle, true);
        }

        base.OnDestroy();
    }

    protected SizeI GetClientRectSize()
    {
        if (Handle != nint.Zero && NativeMethods.GetClientRect(Handle, out NativeMethods.RECT r))
        {
            int width = Math.Max(1, r.right - r.left);
            int height = Math.Max(1, r.bottom - r.top);
            return new SizeI(width, height);
        }
        int baseWidth = Math.Max(1, Width);
        int baseHeight = Math.Max(1, Height);
        if (Handle != nint.Zero)
        {
            Console.WriteLine($"GetClientRect failed. Falling back to stored size: {baseWidth}x{baseHeight}");
        }
        return new SizeI(baseWidth, baseHeight);
    }
}