// Win32Window.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DirectUI.Core; // Added for IWindowHost

namespace DirectUI;

public abstract class Win32Window : IDisposable
{
    private readonly string _windowClassName;
    private readonly string _windowTitle;
    private readonly int _initialWidth;
    private readonly int _initialHeight;
    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _hInstance = IntPtr.Zero;
    private NativeMethods.WndProc _wndProcDelegate;
    private bool _isDisposed = false;
    private static readonly HashSet<string> RegisteredClassNames = new HashSet<string>();
    private GCHandle _gcHandle;

    private readonly Dictionary<uint, Func<IntPtr, IntPtr, IntPtr, IntPtr>> _messageHandlers;

    public IntPtr Handle => _hwnd;
    public string Title => _windowTitle;
    public int Width { get; protected set; }
    public int Height { get; protected set; }
    protected IntPtr OwnerHandle { get; private set; } = IntPtr.Zero;

    protected Win32Window(string title, int width, int height, string className = null)
    {
        _windowTitle = title ?? "Win32 Window";
        _initialWidth = width > 0 ? width : 800;
        _initialHeight = height > 0 ? height : 600;
        Width = _initialWidth;
        Height = _initialHeight;
        _windowClassName = className ?? ("Win32Window_" + Guid.NewGuid().ToString("N"));
        _wndProcDelegate = WindowProcedure;

        _messageHandlers = new Dictionary<uint, Func<IntPtr, IntPtr, IntPtr, IntPtr>>();
        InitializeMessageHandlers();
    }

    private void InitializeMessageHandlers()
    {
        _messageHandlers.Add(NativeMethods.WM_PAINT, Handle_WmPaint);
        _messageHandlers.Add(NativeMethods.WM_SIZE, Handle_WmSize);
        _messageHandlers.Add(NativeMethods.WM_MOUSEMOVE, Handle_WmMouseMove);
        _messageHandlers.Add(NativeMethods.WM_LBUTTONDOWN, (h, w, l) => Handle_WmMouseButton(MouseButton.Left, h, w, l, true));
        _messageHandlers.Add(NativeMethods.WM_LBUTTONUP, (h, w, l) => Handle_WmMouseButton(MouseButton.Left, h, w, l, false));
        _messageHandlers.Add(NativeMethods.WM_RBUTTONDOWN, (h, w, l) => Handle_WmMouseButton(MouseButton.Right, h, w, l, true));
        _messageHandlers.Add(NativeMethods.WM_MBUTTONDOWN, (h, w, l) => Handle_WmMouseButton(MouseButton.Middle, h, w, l, true));
        _messageHandlers.Add(NativeMethods.WM_MBUTTONUP, (h, w, l) => Handle_WmMouseButton(MouseButton.Middle, h, w, l, false));
        _messageHandlers.Add(NativeMethods.WM_XBUTTONDOWN, (h, w, l) => Handle_WmXMouseButton(h, w, l, true));
        _messageHandlers.Add(NativeMethods.WM_XBUTTONUP, (h, w, l) => Handle_WmXMouseButton(h, w, l, false));
        _messageHandlers.Add(NativeMethods.WM_MOUSEWHEEL, Handle_WmMouseWheel);
        _messageHandlers.Add(NativeMethods.WM_KEYDOWN, (h, w, l) => Handle_WmKey(true, h, w, l));
        _messageHandlers.Add(NativeMethods.WM_KEYUP, (h, w, l) => Handle_WmKey(false, h, w, l));
        _messageHandlers.Add(NativeMethods.WM_SYSKEYUP, (h, w, l) => Handle_WmKey(false, h, w, l));
        _messageHandlers.Add(NativeMethods.WM_CHAR, Handle_WmChar);
        _messageHandlers.Add(NativeMethods.WM_CLOSE, Handle_WmClose);
        _messageHandlers.Add(NativeMethods.WM_DESTROY, Handle_WmDestroy);
        _messageHandlers.Add(NativeMethods.WM_NCDESTROY, Handle_WmNcDestroy);
    }

    public bool Create(IntPtr owner = default, uint? style = null, int? x = null, int? y = null)
    {
        OwnerHandle = owner;

        if (_hwnd != IntPtr.Zero)
        {
            return true;
        }

        if (!TryCreateWindow(owner, style, x, y))
        {
            Console.WriteLine("Window creation failed.");
            Dispose();
            return false;
        }

        if (!Initialize())
        {
            Console.WriteLine("Derived init failed.");
            Dispose();
            return false;
        }

        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOWNORMAL);
        NativeMethods.UpdateWindow(_hwnd);

        return true;
    }

    private bool TryCreateWindow(IntPtr owner, uint? style, int? x, int? y)
    {
        _hInstance = NativeMethods.GetModuleHandle(null);

        if (_hInstance == IntPtr.Zero)
        {
            _hInstance = Process.GetCurrentProcess().Handle;
        }

        Win32WindowHost.RegisterWindow(this);

        lock (RegisteredClassNames)
        {
            if (!RegisteredClassNames.Contains(_windowClassName))
            {
                NativeMethods.WNDCLASSEX wndClass = new()
                {
                    cbSize = Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX)),
                    style = NativeMethods.CS_HREDRAW | NativeMethods.CS_VREDRAW,
                    lpfnWndProc = _wndProcDelegate,
                    hInstance = _hInstance,
                    lpszClassName = _windowClassName,
                    hIcon = NativeMethods.LoadIcon(IntPtr.Zero, (IntPtr)32512),
                    hCursor = NativeMethods.LoadCursor(IntPtr.Zero, 32512),
                };

                if (NativeMethods.RegisterClassEx(ref wndClass) == 0)
                {
                    Console.WriteLine($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                RegisteredClassNames.Add(_windowClassName);
                Console.WriteLine($"Class '{_windowClassName}' registered.");
            }
        }

        _gcHandle = GCHandle.Alloc(this, GCHandleType.Normal); // Explicitly specify Normal type

        uint windowStyle = style ?? (NativeMethods.WS_OVERLAPPEDWINDOW | NativeMethods.WS_VISIBLE);

        int finalX = x ?? NativeMethods.CW_USEDEFAULT;
        int finalY = y ?? NativeMethods.CW_USEDEFAULT;

        _hwnd = NativeMethods.CreateWindowEx(
            0,
            _windowClassName,
            _windowTitle,
            windowStyle,
            finalX,
            finalY,
            _initialWidth,
            _initialHeight,
            owner,
            IntPtr.Zero,
            _hInstance,
            GCHandle.ToIntPtr(_gcHandle));

        if (_hwnd == IntPtr.Zero)
        {
            Console.WriteLine($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");

            if (_gcHandle.IsAllocated)
            {
                _gcHandle.Free();
            }

            return false;
        }

        Console.WriteLine($"Window created: {_hwnd}"); return true;
    }

    private static IntPtr WindowProcedure(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        Win32Window? window = null;

        if (msg == NativeMethods.WM_NCCREATE)
        {
            var cs = Marshal.PtrToStructure<NativeMethods.CREATESTRUCT>(lParam);
            var handle = GCHandle.FromIntPtr(cs.lpCreateParams);
            window = handle.Target as Win32Window;

            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, GCHandle.ToIntPtr(handle));
            Console.WriteLine($"WM_NCCREATE: Associated instance with HWND {hWnd}");
        }
        else
        {
            IntPtr ptr = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA);

            if (ptr != IntPtr.Zero)
            {
                var handle = GCHandle.FromIntPtr(ptr);
                window = handle.Target as Win32Window;
            }
        }

        if (window is null)
        {
            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        try
        {
            return window.HandleMessage(hWnd, msg, wParam, lParam);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling msg {msg}: {ex}");
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    protected virtual IntPtr HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (_messageHandlers.TryGetValue(msg, out var handler))
        {
            return handler(hWnd, wParam, lParam);
        }
        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private IntPtr Handle_WmPaint(IntPtr hWnd, IntPtr wParam, IntPtr lParam)
    {
        OnPaint();
        return IntPtr.Zero;
    }

    private IntPtr Handle_WmSize(IntPtr hWnd, IntPtr wParam, IntPtr lParam)
    {
        Width = NativeMethods.LoWord(lParam);
        Height = NativeMethods.HiWord(lParam);
        OnSize(Width, Height);
        return IntPtr.Zero;
    }

    private IntPtr Handle_WmMouseMove(IntPtr hWnd, IntPtr wParam, IntPtr lParam)
    {
        OnMouseMove(NativeMethods.LoWord(lParam), NativeMethods.HiWord(lParam));
        return IntPtr.Zero;
    }

    private IntPtr Handle_WmMouseButton(MouseButton button, IntPtr hWnd, IntPtr wParam, IntPtr lParam, bool isDown)
    {
        if (isDown) NativeMethods.SetCapture(hWnd);
        else NativeMethods.ReleaseCapture();

        if (isDown) OnMouseDown(button, NativeMethods.LoWord(lParam), NativeMethods.HiWord(lParam));
        else OnMouseUp(button, NativeMethods.LoWord(lParam), NativeMethods.HiWord(lParam));
        return IntPtr.Zero;
    }

    private IntPtr Handle_WmXMouseButton(IntPtr hWnd, IntPtr wParam, IntPtr lParam, bool isDown)
    {
        short xButton = NativeMethods.HiWord(wParam);
        MouseButton button = (xButton == 1) ? MouseButton.XButton1 : MouseButton.XButton2;

        if (isDown) NativeMethods.SetCapture(hWnd);
        else NativeMethods.ReleaseCapture();

        if (isDown) OnMouseDown(button, NativeMethods.LoWord(lParam), NativeMethods.HiWord(lParam));
        else OnMouseUp(button, NativeMethods.LoWord(lParam), NativeMethods.HiWord(lParam));
        return IntPtr.Zero;
    }

    private IntPtr Handle_WmMouseWheel(IntPtr hWnd, IntPtr wParam, IntPtr lParam)
    {
        short wheelDelta = NativeMethods.HiWord(wParam);
        OnMouseWheel((float)wheelDelta / 120.0f); // Normalize delta
        return IntPtr.Zero;
    }

    private IntPtr Handle_WmKey(bool isDown, IntPtr hWnd, IntPtr wParam, IntPtr lParam)
    {
        Keys key = (Keys)wParam;
        if (isDown) OnKeyDown(key);
        else OnKeyUp(key);
        return IntPtr.Zero;
    }

    private IntPtr Handle_WmChar(IntPtr hWnd, IntPtr wParam, IntPtr lParam)
    {
        OnChar((char)wParam);
        return IntPtr.Zero;
    }

    private IntPtr Handle_WmClose(IntPtr hWnd, IntPtr wParam, IntPtr lParam)
    {
        if (OnClose())
        {
            NativeMethods.DestroyWindow(hWnd);
        }
        return IntPtr.Zero;
    }

    private IntPtr Handle_WmDestroy(IntPtr hWnd, IntPtr wParam, IntPtr lParam)
    {
        Console.WriteLine($"WM_DESTROY for {_hwnd}.");
        Win32WindowHost.UnregisterWindow(this);
        OnDestroy();

        if (OwnerHandle == IntPtr.Zero)
        {
            Win32WindowHost.Exit();
        }
        return IntPtr.Zero;
    }

    private IntPtr Handle_WmNcDestroy(IntPtr hWnd, IntPtr wParam, IntPtr lParam)
    {
        Console.WriteLine($"WM_NCDESTROY: Releasing GCHandle for {hWnd}.");

        IntPtr ptr = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA);

        if (ptr != IntPtr.Zero)
        {
            var handle = GCHandle.FromIntPtr(ptr);

            if (handle.IsAllocated)
            {
                handle.Free();
            }

            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, IntPtr.Zero);
        }

        if (_gcHandle.IsAllocated)
        {
            _gcHandle.Free();
            _gcHandle = default;
        }

        _hwnd = IntPtr.Zero;
        return IntPtr.Zero;
    }


    public void Close()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        // Post a close message instead of destroying directly.
        // This is a safer pattern, allowing the window to process the close request
        // through its own message loop, preventing re-entrancy issues if called
        // from within a message handler (like a button click during WM_PAINT).
        NativeMethods.PostMessage(_hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    public void Invalidate()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.InvalidateRect(_hwnd, IntPtr.Zero, false);
    }

    public virtual void FrameUpdate()
    {

    }

    internal bool GetWindowRect(out NativeMethods.RECT rect)
    {
        if (Handle == IntPtr.Zero)
        {
            rect = default;
            return false;
        }

        return NativeMethods.GetWindowRect(Handle, out rect);
    }

    protected virtual bool Initialize()
    {
        return true;
    }

    protected abstract void OnPaint();

    protected virtual void OnSize(int width, int height)
    {

    }

    protected virtual void OnMouseDown(MouseButton button, int x, int y)
    {

    }

    protected virtual void OnMouseUp(MouseButton button, int x, int y)
    {

    }

    protected virtual void OnMouseMove(int x, int y)
    {

    }

    protected virtual void OnKeyDown(Keys key)
    {

    }

    protected virtual void OnKeyUp(Keys key)
    {

    }

    protected virtual void OnMouseWheel(float delta)
    {

    }

    protected virtual void OnChar(char c)
    {

    }

    protected virtual bool OnClose()
    {
        return true;
    }

    protected virtual void OnDestroy()
    {

    }

    protected virtual void Cleanup()
    {

    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            Console.WriteLine("Disposing Win32Window (managed)...");
            Cleanup();
        }

        Console.WriteLine("Disposing Win32Window (unmanaged)...");

        if (_hwnd != IntPtr.Zero)
        {
            Console.WriteLine($"Destroying window {_hwnd} during Dispose...");
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
        else
        {
            // If _hwnd is already zero, it means DestroyWindow was likely called via WM_DESTROY/WM_NCDESTROY.
            // In that case, the GCHandle should have already been freed by Handle_WmNcDestroy.
            // Only free it here if it's still allocated and _hwnd is zero, indicating a Dispose() call
            // that didn't go through the full Win32 message loop shutdown for some reason.
            if (_gcHandle.IsAllocated)
            {
                Console.WriteLine("Freeing dangling GCHandle during Dispose (unexpected, but cleaning up).");
                _gcHandle.Free();
                _gcHandle = default;
            }
        }

        _isDisposed = true;

        Console.WriteLine("Win32Window disposed.");
    }

    ~Win32Window()
    {
        Console.WriteLine("Win32Window Finalizer!");
        Dispose(false);
    }
}