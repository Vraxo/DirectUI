using DirectUI.Core;
using DirectUI.Input;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI;

public class Win32WindowHost : Win32Window, IWindowHost, IModalWindowService
{
    private AppServices? _appServices;

    // State for managing overlay modals
    private bool _isModalActive;
    private Rect _currentModalBounds; // Bounds of the modal overlay relative to the main window
    private Action<UIContext>? _currentModalDrawCallback;
    private Action<int>? _currentOnModalClosedCallback;
    private int _currentModalResultCode;

    public bool IsModalWindowOpen => _isModalActive;

    public Win32WindowHost(string title = "DirectUI Win32 Host", int width = 800, int height = 600)
        : base(title, width, height)
    {
    }

    public InputManager Input => _appServices?.AppEngine.Input ?? new();
    public SizeI ClientSize => GetClientRectSize();

    public bool ShowFpsCounter
    {
        get
        {
            return _appServices?.AppEngine.ShowFpsCounter ?? false;
        }

        set
        {
            if (_appServices is null)
            {
                return;
            }

            _appServices.AppEngine.ShowFpsCounter = value;
        }
    }

    public IModalWindowService ModalWindowService => this;

    public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        Console.WriteLine("Win32WindowHost initializing...");

        if (!base.Initialize())
        {
            return false;
        }

        try
        {
            _appServices = Win32AppServicesInitializer.Initialize(Handle, GetClientRectSize(), uiDrawCallback, backgroundColor);
            _isModalActive = false;
            _currentModalDrawCallback = null;
            _currentOnModalClosedCallback = null;
            _currentModalResultCode = 0;
            _currentModalBounds = default;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Win32WindowHost services: {ex.Message}");
            return false;
        }
    }

    void IWindowHost.Cleanup()
    {
        Cleanup();
    }

    protected override void Cleanup()
    {
        Console.WriteLine("Win32WindowHost cleaning up its resources...");
        _appServices?.AppEngine.Cleanup();
        _appServices?.TextService.Cleanup();
        (_appServices?.Renderer as DirectUI.Backends.Direct2DRenderer)?.Cleanup();
        _appServices?.GraphicsDevice.Cleanup();

        _appServices = null; // Clear the bundle

        base.Cleanup();
    }

    public void RunLoop()
    {
        Application.RunMessageLoop();
    }

    protected override void OnPaint()
    {
        if (_appServices is null || !_appServices.GraphicsDevice.IsInitialized)
        {
            Console.WriteLine("Render services not initialized. Skipping paint.");
            return;
        }

        _appServices.GraphicsDevice.BeginDraw();

        try
        {
            if (_isModalActive)
            {
                // Draw a dimming overlay first, covering the entire window
                _appServices.Renderer.DrawBox(
                    new Rect(0, 0, _appServices.Renderer.RenderTargetSize.X, _appServices.Renderer.RenderTargetSize.Y),
                    new BoxStyle { FillColor = new Color4(0, 0, 0, 0.5f), Roundness = 0f, BorderLength = 0f });

                // Then, draw the modal's content by providing its specific draw callback to the AppEngine
                // The AppEngine will handle BeginFrame/EndFrame, layout, and actual drawing.
                // The modal content itself will draw within _currentModalBounds.
                // This call effectively replaces the main UI rendering for this frame.
                _appServices.AppEngine.UpdateAndRenderModal(_appServices.Renderer, _appServices.TextService,
                    (ctx) =>
                    {
                        // Push a layout origin for the modal's internal UI elements
                        // to draw relative to 0,0 for its content area.
                        ctx.Layout.PushLayoutOrigin(_currentModalBounds.TopLeft);

                        // Push a clip rect for the modal's content area
                        ctx.Renderer.PushClipRect(_currentModalBounds);
                        ctx.Layout.PushClipRect(_currentModalBounds);

                        _currentModalDrawCallback?.Invoke(ctx);

                        // Pop clip rect and layout origin
                        ctx.Layout.PopClipRect();
                        ctx.Renderer.PopClipRect();
                        ctx.Layout.PopLayoutOrigin();
                    }
                );
            }
            else
            {
                // If no modal is active, draw the main UI
                _appServices.AppEngine.UpdateAndRender(_appServices.Renderer, _appServices.TextService);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during drawing: {ex}");
            _appServices.GraphicsDevice.Cleanup();
        }
        finally
        {
            _appServices.GraphicsDevice.EndDraw();
        }
    }

    public override void FrameUpdate()
    {
        Invalidate();
        // The modal lifecycle is now fully managed by the IModalWindowService implementation
        // and the AppEngine's conditional rendering in OnPaint.
    }

    protected override void OnSize(int width, int height)
    {
        _appServices?.GraphicsDevice.Resize(new SizeI(width, height));
    }

    protected override void OnMouseMove(int x, int y)
    {
        _appServices?.AppEngine.Input.SetMousePosition(x, y);
        Invalidate();
    }

    protected override void OnMouseDown(MouseButton button, int x, int y)
    {
        _appServices?.AppEngine.Input.SetMousePosition(x, y);
        _appServices?.AppEngine.Input.SetMouseDown(button);
        Invalidate();
    }

    protected override void OnMouseUp(MouseButton button, int x, int y)
    {
        _appServices?.AppEngine.Input.SetMousePosition(x, y);
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
            if (IsModalWindowOpen)
            {
                CloseModalWindow(-1); // Escape closes modal with a cancel result
            }
            else
            {
                Close(); // Escape closes main window if no modal is open
            }
        }

        if (key == Keys.F3 && _appServices?.AppEngine is not null)
        {
            _appServices.AppEngine.ShowFpsCounter = !_appServices.AppEngine.ShowFpsCounter;
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

    protected override bool OnClose()
    {
        return true;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        // Ensure the modal state is reset if the main window is destroyed while a modal is active.
        if (_isModalActive)
        {
            // Do not call CloseModalWindow as it tries to re-enable main window, which is being destroyed.
            _isModalActive = false;
            _currentModalDrawCallback = null;
            _currentOnModalClosedCallback = null;
            _currentModalBounds = default;
            // No explicit UI.State.ClearActivePopup because window destruction implies UI context cleanup.
        }
    }

    private SizeI GetClientRectSize()
    {
        if (Handle == nint.Zero || !NativeMethods.GetClientRect(Handle, out NativeMethods.RECT r))
        {
            return new(Width, Height);
        }

        int width = Math.Max(1, r.right - r.left);
        int height = Math.Max(1, r.bottom - r.top);

        return new(width, height);
    }

    public void OpenModalWindow(string title, int width, int height, Action<UIContext> drawCallback, Action<int>? onClosedCallback = null)
    {
        if (_isModalActive)
        {
            Console.WriteLine("Warning: Cannot open a new modal window while another is already active.");
            onClosedCallback?.Invoke(-1);
            return;
        }

        // Calculate modal position to center it over the main window
        var clientSize = ClientSize;
        float modalX = (clientSize.Width - width) / 2f;
        float modalY = (clientSize.Height - height) / 2f;
        _currentModalBounds = new Rect(modalX, modalY, width, height);

        _currentModalDrawCallback = drawCallback;
        _currentOnModalClosedCallback = onClosedCallback;
        _currentModalResultCode = -1; // Default to cancel/error

        _isModalActive = true;

        // Disable interaction with the main window until the modal is closed
        NativeMethods.EnableWindow(Handle, false);
        Invalidate(); // Ensure a repaint happens to show the modal
    }

    public void CloseModalWindow(int resultCode = 0)
    {
        if (!_isModalActive)
        {
            return;
        }

        _currentModalResultCode = resultCode;
        _currentOnModalClosedCallback?.Invoke(_currentModalResultCode);

        // Re-enable the main window
        NativeMethods.EnableWindow(Handle, true);

        // Clear local modal state
        _isModalActive = false;
        _currentModalDrawCallback = null;
        _currentOnModalClosedCallback = null;
        _currentModalBounds = default;

        Invalidate(); // Ensure a repaint happens
    }
}