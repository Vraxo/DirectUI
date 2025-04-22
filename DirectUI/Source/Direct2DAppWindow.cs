// MODIFIED: Direct2DAppWindow.cs
// Summary: Changed PresentOptions from None to Immediately in HwndRenderTargetProperties to attempt disabling VSync throttling.
using System;
using System.Numerics;
using System.Diagnostics;

using Vortice;
using Vortice.Mathematics;

using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using SharpGen.Runtime;

using D2D = Vortice.Direct2D1;
using DW = Vortice.DirectWrite;

using D2DFactoryType = Vortice.Direct2D1.FactoryType;
using SizeI = Vortice.Mathematics.SizeI;
using Rect = Vortice.Mathematics.Rect;

using DirectUI;
using Vortice.DCommon;

namespace DirectUI;

public abstract class Direct2DAppWindow : Win32Window
{
    protected ID2D1Factory1? d2dFactory;
    protected IDWriteFactory? dwriteFactory;
    protected ID2D1HwndRenderTarget? renderTarget;

    protected Color4 backgroundColor = new(0.1f, 0.1f, 0.15f, 1.0f);
    protected bool graphicsInitialized = false;

    protected Vector2 currentMousePos = new(-1, -1);
    protected bool isLeftMouseButtonDown = false;
    protected bool wasLeftMouseClickedThisFrame = false;

    // --- FPS Counter Fields ---
    private Stopwatch fpsTimer = new();
    private long lastFpsUpdateTimeTicks = 0;
    private int frameCountSinceUpdate = 0;
    private float currentFps = 0.0f;
    private const long FpsUpdateIntervalTicks = TimeSpan.TicksPerSecond / 2;
    private ID2D1SolidColorBrush? fpsTextBrush;
    private IDWriteTextFormat? fpsTextFormat;
    private readonly Color4 fpsTextColor = DefaultTheme.Text;
    private readonly string fpsFontName = "Consolas";
    private readonly float fpsFontSize = 14.0f;
    // --- End FPS Counter Fields ---

    public Direct2DAppWindow(string title = "Vortice DirectUI Base Window", int width = 800, int height = 600)
        : base(title, width, height)
    { }

    protected override bool Initialize()
    {
        Console.WriteLine("Direct2DAppWindow initializing Vortice Graphics...");
        return InitializeGraphics();
    }

    protected override void Cleanup()
    {
        Console.WriteLine("Direct2DAppWindow cleaning up its resources...");
        CleanupGraphics();
    }

    protected override void OnPaint()
    {
        // --- FPS Timer Update ---
        if (!fpsTimer.IsRunning)
        {
            fpsTimer.Start();
            lastFpsUpdateTimeTicks = fpsTimer.ElapsedTicks;
            frameCountSinceUpdate = 0;
        }

        frameCountSinceUpdate++;
        long elapsedTicks = fpsTimer.ElapsedTicks;
        long timeSinceLastUpdate = elapsedTicks - lastFpsUpdateTimeTicks;

        if (timeSinceLastUpdate >= FpsUpdateIntervalTicks)
        {
            float secondsElapsed = (float)timeSinceLastUpdate / TimeSpan.TicksPerSecond;
            currentFps = (secondsElapsed > 0.001f) ? (frameCountSinceUpdate / secondsElapsed) : 0.0f;
            frameCountSinceUpdate = 0;
            lastFpsUpdateTimeTicks = elapsedTicks;
            Invalidate();
        }
        // --- End FPS Timer Update ---


        if (!graphicsInitialized || renderTarget is null || dwriteFactory is null)
        {
            if (!graphicsInitialized && Handle != nint.Zero)
            {
                InitializeGraphics();
                if (!graphicsInitialized || renderTarget is null || dwriteFactory is null)
                {
                    wasLeftMouseClickedThisFrame = false;
                    return;
                }
            }
            else
            {
                wasLeftMouseClickedThisFrame = false;
                return;
            }
        }

        try
        {
            renderTarget.BeginDraw();
            renderTarget.Clear(backgroundColor);

            var inputState = new InputState(
                currentMousePos,
                wasLeftMouseClickedThisFrame,
                isLeftMouseButtonDown
            );

            var drawingContext = new DrawingContext(renderTarget, dwriteFactory);

            // --- Draw Main UI Content ---
            DrawUIContent(drawingContext, inputState);

            // --- Draw FPS Counter ---
            if (fpsTextBrush is not null && fpsTextFormat is not null)
            {
                string fpsText = $"FPS: {currentFps:F1}";
                Rect fpsLayoutRect = new Rect(5f, 5f, 150f, 30f);
                renderTarget.DrawText(fpsText, fpsTextFormat, fpsLayoutRect, fpsTextBrush);
            }
            // --- End Draw FPS Counter ---


            Result endDrawResult = renderTarget.EndDraw();

            if (endDrawResult.Failure)
            {
                Console.WriteLine($"EndDraw failed: {endDrawResult.Description}");
                if (endDrawResult.Code == D2D.ResultCode.RecreateTarget.Code)
                {
                    Console.WriteLine("Render target needs recreation (Detected in EndDraw).");
                    graphicsInitialized = false;
                    CleanupGraphics();
                }
            }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == D2D.ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Render target needs recreation (Caught SharpGenException in OnPaint): {ex.Message}");
            graphicsInitialized = false;
            CleanupGraphics();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Rendering Error: {ex}");
            graphicsInitialized = false;
            CleanupGraphics();
        }
        finally
        {
            wasLeftMouseClickedThisFrame = false;
        }
    }

    protected virtual void DrawUIContent(DrawingContext context, InputState input)
    {
        // Base implementation does nothing.
    }

    protected override void OnSize(int width, int height)
    {
        if (graphicsInitialized && renderTarget is not null)
        {
            Console.WriteLine($"Window resized to {width}x{height}. Resizing render target...");
            try
            {
                var newPixelSize = new SizeI(width, height);
                fpsTextBrush?.Dispose();
                fpsTextBrush = null;

                renderTarget.Resize(newPixelSize);

                try
                {
                    if (renderTarget is not null)
                    {
                        fpsTextBrush = renderTarget.CreateSolidColorBrush(fpsTextColor);
                        Console.WriteLine("Recreated FPS brush after resize.");
                    }
                }
                catch (Exception brushEx)
                {
                    Console.WriteLine($"Warning: Failed to recreate FPS brush after resize: {brushEx.Message}");
                }

                Console.WriteLine($"Successfully resized render target.");
            }
            catch (SharpGenException ex)
            {
                Console.WriteLine($"Failed to resize Render Target (SharpGenException): {ex.Message} HRESULT: {ex.ResultCode}");
                if (ex.ResultCode.Code == D2D.ResultCode.RecreateTarget.Code)
                {
                    Console.WriteLine("Render target needs recreation (Detected in Resize Exception).");
                    graphicsInitialized = false;
                    CleanupGraphics();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to resize Render Target (General Exception): {ex}");
                graphicsInitialized = false;
                CleanupGraphics();
            }
        }
        else if (!graphicsInitialized && Handle != nint.Zero)
        {
            InitializeGraphics();
        }
    }

    protected override void OnMouseMove(int x, int y) { currentMousePos = new Vector2(x, y); Invalidate(); }

    protected override void OnMouseDown(MouseButton button, int x, int y)
    {
        currentMousePos = new Vector2(x, y);
        if (button == MouseButton.Left)
        {
            isLeftMouseButtonDown = true;
            wasLeftMouseClickedThisFrame = true;
        }
        Invalidate();
    }

    protected override void OnMouseUp(MouseButton button, int x, int y)
    {
        currentMousePos = new Vector2(x, y);
        if (button == MouseButton.Left)
        {
            isLeftMouseButtonDown = false;
        }
        Invalidate();
    }

    protected override void OnKeyDown(int keyCode)
    {
        if (keyCode == NativeMethods.VK_ESCAPE)
        {
            Close();
        }
        Invalidate();
    }

    protected override bool OnClose() { return true; }

    protected virtual bool InitializeGraphics()
    {
        if (graphicsInitialized) return true;
        if (Handle == nint.Zero) return false;

        Console.WriteLine($"Attempting Graphics Initialization for HWND {Handle}...");

        try
        {
            CleanupGraphics();

            Result factoryResult = D2D1.D2D1CreateFactory(D2DFactoryType.SingleThreaded, out d2dFactory);
            factoryResult.CheckError();
            if (d2dFactory is null) throw new InvalidOperationException("D2D Factory creation failed silently.");

            Result dwriteResult = DWrite.DWriteCreateFactory(DW.FactoryType.Shared, out dwriteFactory);
            dwriteResult.CheckError();
            if (dwriteFactory is null) throw new InvalidOperationException("DWrite Factory creation failed silently.");

            var clientRectSize = GetClientRectSize();
            if (clientRectSize.Width <= 0 || clientRectSize.Height <= 0)
            {
                Console.WriteLine($"Invalid client rect size ({clientRectSize.Width}x{clientRectSize.Height}). Aborting graphics initialization.");
                CleanupGraphics();
                return false;
            }

            var dxgiPixelFormat = new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);
            var renderTargetProperties = new RenderTargetProperties(dxgiPixelFormat);
            var hwndRenderTargetProperties = new HwndRenderTargetProperties
            {
                Hwnd = Handle,
                PixelSize = new SizeI(clientRectSize.Width, clientRectSize.Height),
                // --- CHANGE HERE ---
                PresentOptions = PresentOptions.Immediately // Attempt to disable VSync throttling
                // --- END CHANGE ---
            };

            renderTarget = d2dFactory.CreateHwndRenderTarget(renderTargetProperties, hwndRenderTargetProperties);
            if (renderTarget is null) throw new InvalidOperationException("Render target creation returned null unexpectedly.");

            renderTarget.TextAntialiasMode = D2D.TextAntialiasMode.Cleartype;

            // --- Initialize FPS Resources ---
            try
            {
                fpsTextFormat?.Dispose();
                fpsTextFormat = dwriteFactory.CreateTextFormat(fpsFontName, null, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, fpsFontSize, "en-us");
                fpsTextFormat.TextAlignment = DW.TextAlignment.Leading;
                fpsTextFormat.ParagraphAlignment = ParagraphAlignment.Near;

                fpsTextBrush?.Dispose();
                fpsTextBrush = renderTarget.CreateSolidColorBrush(fpsTextColor);
                Console.WriteLine("Created FPS drawing resources.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to create FPS drawing resources: {ex.Message}");
                fpsTextFormat?.Dispose(); fpsTextFormat = null;
                fpsTextBrush?.Dispose(); fpsTextBrush = null;
            }
            // Reset and Start timer
            frameCountSinceUpdate = 0;
            currentFps = 0;
            lastFpsUpdateTimeTicks = 0;
            fpsTimer.Restart();
            // --- End Initialize FPS Resources ---


            Console.WriteLine($"Vortice Graphics initialized successfully for HWND {Handle}.");
            graphicsInitialized = true;
            return true;
        }
        catch (SharpGenException ex)
        {
            Console.WriteLine($"Graphics Initialization failed (SharpGenException): {ex.Message} HRESULT: {ex.ResultCode}");
            CleanupGraphics(); graphicsInitialized = false; return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Graphics Initialization failed (General Exception): {ex}");
            CleanupGraphics(); graphicsInitialized = false; return false;
        }
    }

    protected virtual void CleanupGraphics()
    {
        bool resourcesExisted = d2dFactory is not null || renderTarget is not null || dwriteFactory is not null;
        if (resourcesExisted) Console.WriteLine("Cleaning up Vortice Graphics resources...");

        fpsTimer.Stop();

        fpsTextBrush?.Dispose(); fpsTextBrush = null;
        fpsTextFormat?.Dispose(); fpsTextFormat = null;

        UI.CleanupResources();

        renderTarget?.Dispose(); renderTarget = null;
        dwriteFactory?.Dispose(); dwriteFactory = null;
        d2dFactory?.Dispose(); d2dFactory = null;
        graphicsInitialized = false;

        if (resourcesExisted) Console.WriteLine("Finished cleaning graphics resources.");
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