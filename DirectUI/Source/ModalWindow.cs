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
    private AppEngine? _appEngine;
    private DuiGraphicsDevice? _graphicsDevice;
    private IRenderer? _renderer;
    private ITextService? _textService;

    public ModalWindow(Win32Window owner, string title, int width, int height, Action<UIContext> drawCallback)
        : base(title, width, height)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _drawCallback = drawCallback ?? throw new ArgumentNullException(nameof(drawCallback));
    }

    protected override bool Initialize()
    {
        Console.WriteLine("ModalWindow initializing...");
        _appEngine = new AppEngine(_drawCallback, new Color4(37 / 255f, 37 / 255f, 38 / 255f, 1.0f));

        _graphicsDevice = new DuiGraphicsDevice();
        if (!_graphicsDevice.Initialize(Handle, GetClientRectSize()))
        {
            Console.WriteLine("Failed to initialize DuiGraphicsDevice for modal window.");
            return false;
        }

        // Initialize backend services using the concrete D2D and DWrite factories
        if (_graphicsDevice.RenderTarget != null && _graphicsDevice.DWriteFactory != null)
        {
            _renderer = new DirectUI.Backends.Direct2DRenderer(_graphicsDevice.RenderTarget, _graphicsDevice.DWriteFactory);
            _textService = new DirectUI.Backends.DirectWriteTextService(_graphicsDevice.DWriteFactory);
        }
        else
        {
            Console.WriteLine("CRITICAL: GraphicsDevice did not provide valid RenderTarget or DWriteFactory for modal window initialization.");
            return false;
        }

        _appEngine.Initialize(_textService, _renderer); // Initialize AppEngine's internal components
        return true;
    }

    protected override void Cleanup()
    {
        Console.WriteLine("ModalWindow cleaning up its resources...");
        _appEngine?.Cleanup();
        _textService?.Cleanup();
        (_renderer as DirectUI.Backends.Direct2DRenderer)?.Cleanup();
        _graphicsDevice?.Cleanup();

        _appEngine = null;
        _renderer = null;
        _textService = null;
        _graphicsDevice = null;
    }

    protected override void OnPaint()
    {
        if (_graphicsDevice is null || _renderer is null || _textService is null || _appEngine is null)
        {
            Console.WriteLine("Modal window render services not initialized. Skipping paint.");
            return;
        }

        _graphicsDevice.BeginDraw();
        try
        {
            // Pass the modal's specific renderer and text service to the AppEngine
            _appEngine.UpdateAndRender(_renderer, _textService);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during modal window drawing: {ex}");
            _graphicsDevice.Cleanup();
        }
        finally
        {
            _graphicsDevice.EndDraw();
        }
    }

    public override void FrameUpdate()
    {
        Invalidate(); // Always invalidate to trigger a paint message for a continuous render loop.
    }

    protected override void OnSize(int width, int height)
    {
        _graphicsDevice?.Resize(new SizeI(width, height));
    }

    // Input handlers for the modal window pass input to its own AppEngine's InputManager.
    protected override void OnMouseMove(int x, int y)
    {
        _appEngine?.Input.SetMousePosition(x, y);
        Invalidate();
    }

    protected override void OnMouseDown(MouseButton button, int x, int y)
    {
        _appEngine?.Input.SetMousePosition(x, y); // Update position on click
        _appEngine?.Input.SetMouseDown(button);
        Invalidate();
    }

    protected override void OnMouseUp(MouseButton button, int x, int y)
    {
        _appEngine?.Input.SetMousePosition(x, y); // Update position on release
        _appEngine?.Input.SetMouseUp(button);
        Invalidate();
    }

    protected override void OnMouseWheel(float delta)
    {
        _appEngine?.Input.AddMouseWheelDelta(delta);
        Invalidate();
    }

    protected override void OnKeyDown(Keys key)
    {
        _appEngine?.Input.AddKeyPressed(key);

        if (key == Keys.Escape)
        {
            Close(); // Close on Escape
        }
        Invalidate();
    }

    protected override void OnKeyUp(Keys key)
    {
        _appEngine?.Input.AddKeyReleased(key);
        Invalidate();
    }

    protected override void OnChar(char c)
    {
        _appEngine?.Input.AddCharacterInput(c);
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
