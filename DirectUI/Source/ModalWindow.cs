using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI;

public class ModalWindow : Win32Window
{
    private readonly Win32Window owner;
    private readonly Action<UIContext> drawCallback;
    private AppServices? appServices;

    public ModalWindow(Win32Window owner, string title, int width, int height, Action<UIContext> drawCallback)
        : base(title, width, height)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.drawCallback = drawCallback ?? throw new ArgumentNullException(nameof(drawCallback));
    }

    protected override bool Initialize()
    {
        Console.WriteLine("ModalWindow initializing...");

        try
        {
            appServices = Win32AppServicesInitializer.Initialize(
                Handle,
                GetClientRectSize(),
                drawCallback,
                new(37 / 255f, 37 / 255f, 38 / 255f, 1.0f));

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
        appServices?.AppEngine.Cleanup();
        appServices?.TextService.Cleanup();
        (appServices?.Renderer as Backends.Direct2DRenderer)?.Cleanup();
        appServices?.GraphicsDevice.Cleanup();

        appServices = null;
    }

    protected override void OnPaint()
    {
        if (appServices is null || !appServices.GraphicsDevice.IsInitialized)
        {
            Console.WriteLine("Modal window render services not initialized. Skipping paint.");
            return;
        }

        appServices.GraphicsDevice.BeginDraw();

        try
        {
            if (appServices.AppEngine is not null && appServices.GraphicsDevice.RenderTarget is not null)
            {
                appServices.GraphicsDevice.RenderTarget.Clear(appServices.AppEngine.BackgroundColor);
            }

            appServices?.AppEngine?.UpdateAndRender(appServices.Renderer, appServices.TextService);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during modal window drawing: {ex}");
            appServices.GraphicsDevice.Cleanup();
        }
        finally
        {
            appServices.GraphicsDevice.EndDraw();
        }
    }

    public override void FrameUpdate()
    {
        Invalidate();
    }

    protected override void OnSize(int width, int height)
    {
        appServices?.GraphicsDevice.Resize(new SizeI(width, height));
    }

    protected override void OnMouseMove(int x, int y)
    {
        appServices?.AppEngine.Input.SetMousePosition(x, y);
        Invalidate();
    }

    protected override void OnMouseDown(MouseButton button, int x, int y)
    {
        appServices?.AppEngine.Input.SetMousePosition(x, y);
        appServices?.AppEngine.Input.SetMouseDown(button);
        Invalidate();
    }

    protected override void OnMouseUp(MouseButton button, int x, int y)
    {
        appServices?.AppEngine.Input.SetMousePosition(x, y);
        appServices?.AppEngine.Input.SetMouseUp(button);
        Invalidate();
    }

    protected override void OnMouseWheel(float delta)
    {
        appServices?.AppEngine.Input.AddMouseWheelDelta(delta);
        Invalidate();
    }

    protected override void OnKeyDown(Keys key)
    {
        appServices?.AppEngine.Input.AddKeyPressed(key);

        if (key == Keys.Escape)
        {
            Close();
        }

        Invalidate();
    }

    protected override void OnKeyUp(Keys key)
    {
        appServices?.AppEngine.Input.AddKeyReleased(key);
        Invalidate();
    }

    protected override void OnChar(char c)
    {
        appServices?.AppEngine.Input.AddCharacterInput(c);
        Invalidate();
    }

    protected override bool OnClose() 
    { 
        return true; 
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

        if (owner.Handle != IntPtr.Zero && owner.GetWindowRect(out NativeMethods.RECT ownerRect))
        {
            int ownerWidth = ownerRect.right - ownerRect.left;
            int ownerHeight = ownerRect.bottom - ownerRect.top;
            int modalWidth = Width;
            int modalHeight = Height;

            x = ownerRect.left + (ownerWidth - modalWidth) / 2;
            y = ownerRect.top + (ownerHeight - modalHeight) / 2;
        }

        if (!Create(owner.Handle, style, x, y))
        {
            return false;
        }

        if (Handle == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.EnableWindow(owner.Handle, false);

        return true;
    }

    protected override void OnDestroy()
    {
        if (owner.Handle != IntPtr.Zero)
        {
            NativeMethods.EnableWindow(owner.Handle, true);
        }

        base.OnDestroy();
    }

    protected SizeI GetClientRectSize()
    {
        if (Handle != nint.Zero && NativeMethods.GetClientRect(Handle, out NativeMethods.RECT r))
        {
            int width = int.Max(1, r.right - r.left);
            int height = int.Max(1, r.bottom - r.top);
            return new(width, height);
        }

        int baseWidth = int.Max(1, Width);
        int baseHeight = int.Max(1, Height);
        
        if (Handle != nint.Zero)
        {
            Console.WriteLine($"GetClientRect failed. Falling back to stored size: {baseWidth}x{baseHeight}");
        }

        return new(baseWidth, baseHeight);
    }
}