using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace DirectUI;

public abstract class Win32Window : IDisposable
{
    // --- Ensure this class is identical to the previous version ---
    // Including:
    // Fields: _windowClassName, _windowTitle, _initialWidth, _initialHeight, _hwnd, _hInstance, _wndProcDelegate, _isDisposed, RegisteredClassNames, _gcHandle
    // Properties: Handle, Width, Height
    // Constructor: Win32Window(...)
    // Methods: Run(), TryCreateWindow(), WindowProcedure(static), HandleMessage(virtual), Close(), Invalidate()
    // Virtual Methods: Initialize(), OnPaint(), OnSize(), OnMouseDown(), OnMouseUp(), OnMouseMove(), OnKeyDown(), OnClose(), OnDestroy(), Cleanup()
    // IDisposable: Dispose(), Dispose(bool), Finalizer ~Win32Window()

    // --- Class Members (example subset) ---
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

    public IntPtr Handle => _hwnd;
    public int Width { get; protected set; } // Changed to protected set
    public int Height { get; protected set; } // Changed to protected set

    // --- Constructor ---
    protected Win32Window(string title, int width, int height, string className = null)
    {
        _windowTitle = title ?? "Win32 Window";
        _initialWidth = width > 0 ? width : 800;
        _initialHeight = height > 0 ? height : 600;
        Width = _initialWidth;
        Height = _initialHeight;
        _windowClassName = className ?? ("Win32Window_" + Guid.NewGuid().ToString("N"));
        _wndProcDelegate = WindowProcedure; // Pin delegate
    }

    // --- Methods (Ensure Run, TryCreateWindow, WindowProcedure, HandleMessage are as before) ---
    public void Run()
    { /* ... as before ... */
        if (_hwnd != IntPtr.Zero) throw new InvalidOperationException("Window already created.");
        if (!TryCreateWindow()) { Console.WriteLine("Window creation failed."); Dispose(); return; }
        if (!Initialize()) { Console.WriteLine("Derived init failed."); Dispose(); return; }
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOWNORMAL);
        NativeMethods.UpdateWindow(_hwnd);
        NativeMethods.MSG msg;
        while (NativeMethods.GetMessage(out msg, IntPtr.Zero, 0, 0))
        { NativeMethods.TranslateMessage(ref msg); NativeMethods.DispatchMessage(ref msg); }
        Console.WriteLine("Exiting message loop.");
    }

    private bool TryCreateWindow()
    { /* ... as before ... */
        _hInstance = NativeMethods.GetModuleHandle(null);
        if (_hInstance == IntPtr.Zero) _hInstance = Process.GetCurrentProcess().Handle;
        lock (RegisteredClassNames)
        {
            if (!RegisteredClassNames.Contains(_windowClassName))
            {
                var wndClass = new NativeMethods.WNDCLASSEX
                { /* ... fill struct ... */
                    cbSize = Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX)),
                    style = NativeMethods.CS_HREDRAW | NativeMethods.CS_VREDRAW,
                    lpfnWndProc = _wndProcDelegate,
                    hInstance = _hInstance,
                    lpszClassName = _windowClassName,
                    hIcon = NativeMethods.LoadIcon(IntPtr.Zero, (IntPtr)32512),
                    hCursor = NativeMethods.LoadCursor(IntPtr.Zero, 32512),
                };
                if (NativeMethods.RegisterClassEx(ref wndClass) == 0) { Console.WriteLine($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}"); return false; }
                RegisteredClassNames.Add(_windowClassName); Console.WriteLine($"Class '{_windowClassName}' registered.");
            }
        }
        _gcHandle = GCHandle.Alloc(this);
        _hwnd = NativeMethods.CreateWindowEx(0, _windowClassName, _windowTitle, NativeMethods.WS_OVERLAPPEDWINDOW | NativeMethods.WS_VISIBLE,
            NativeMethods.CW_USEDEFAULT, NativeMethods.CW_USEDEFAULT, _initialWidth, _initialHeight, IntPtr.Zero, IntPtr.Zero, _hInstance, GCHandle.ToIntPtr(_gcHandle));
        if (_hwnd == IntPtr.Zero) { Console.WriteLine($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}"); if (_gcHandle.IsAllocated) _gcHandle.Free(); return false; }
        Console.WriteLine($"Window created: {_hwnd}"); return true;
    }

    private static IntPtr WindowProcedure(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    { /* ... as before ... */
        Win32Window window = null;
        if (msg == NativeMethods.WM_NCCREATE)
        {
            var cs = Marshal.PtrToStructure<NativeMethods.CREATESTRUCT>(lParam);
            var handle = GCHandle.FromIntPtr(cs.lpCreateParams); window = handle.Target as Win32Window;
            NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, GCHandle.ToIntPtr(handle));
            Console.WriteLine($"WM_NCCREATE: Associated instance with HWND {hWnd}");
        }
        else
        {
            IntPtr ptr = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA);
            if (ptr != IntPtr.Zero) { var handle = GCHandle.FromIntPtr(ptr); window = handle.Target as Win32Window; }
        }
        if (window != null) { try { return window.HandleMessage(hWnd, msg, wParam, lParam); } catch (Exception ex) { Console.WriteLine($"Error handling msg {msg}: {ex}"); } }
        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    protected virtual IntPtr HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    { /* ... as before ... */
        switch (msg)
        {
            case NativeMethods.WM_PAINT: OnPaint(); return IntPtr.Zero;
            case NativeMethods.WM_SIZE: Width = NativeMethods.LoWord(lParam); Height = NativeMethods.HiWord(lParam); OnSize(Width, Height); return IntPtr.Zero;
            case NativeMethods.WM_MOUSEMOVE: OnMouseMove(NativeMethods.LoWord(lParam), NativeMethods.HiWord(lParam)); return IntPtr.Zero;
            case NativeMethods.WM_LBUTTONDOWN:
                NativeMethods.SetCapture(hWnd);
                OnMouseDown(MouseButton.Left, NativeMethods.LoWord(lParam), NativeMethods.HiWord(lParam));
                return IntPtr.Zero;
            case NativeMethods.WM_LBUTTONUP:
                NativeMethods.ReleaseCapture();
                OnMouseUp(MouseButton.Left, NativeMethods.LoWord(lParam), NativeMethods.HiWord(lParam));
                return IntPtr.Zero;
            case NativeMethods.WM_KEYDOWN: OnKeyDown((int)wParam); return IntPtr.Zero;
            case NativeMethods.WM_CLOSE: if (OnClose()) { NativeMethods.DestroyWindow(hWnd); } return IntPtr.Zero;
            case NativeMethods.WM_DESTROY: Console.WriteLine($"WM_DESTROY for {hWnd}."); OnDestroy(); NativeMethods.PostQuitMessage(0); return IntPtr.Zero;
            case NativeMethods.WM_NCDESTROY: Console.WriteLine($"WM_NCDESTROY: Releasing GCHandle for {hWnd}."); IntPtr ptr = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA); if (ptr != IntPtr.Zero) { var handle = GCHandle.FromIntPtr(ptr); if (handle.IsAllocated) handle.Free(); NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, IntPtr.Zero); } if (_gcHandle.IsAllocated && GCHandle.ToIntPtr(_gcHandle) == ptr) { _gcHandle = default; } _hwnd = IntPtr.Zero; return IntPtr.Zero;
            default: return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }

    public void Close() { if (_hwnd != IntPtr.Zero) NativeMethods.DestroyWindow(_hwnd); }
    public void Invalidate() { if (_hwnd != IntPtr.Zero) NativeMethods.InvalidateRect(_hwnd, IntPtr.Zero, false); }

    // --- Virtual Methods (Ensure all needed methods are declared) ---
    protected virtual bool Initialize() { return true; }
    protected virtual void OnPaint() { }
    protected virtual void OnSize(int width, int height) { }
    protected virtual void OnMouseDown(MouseButton button, int x, int y) { }
    protected virtual void OnMouseUp(MouseButton button, int x, int y) { }     // Ensure exists
    protected virtual void OnMouseMove(int x, int y) { }     // Ensure exists
    protected virtual void OnKeyDown(int keyCode) { }
    protected virtual bool OnClose() { return true; }
    protected virtual void OnDestroy() { }
    protected virtual void Cleanup() { }

    // --- IDisposable (Ensure implementation is as before) ---
    public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
    protected virtual void Dispose(bool disposing)
    { /* ... as before ... */
        if (!_isDisposed)
        {
            if (disposing) { Console.WriteLine("Disposing Win32Window (managed)..."); Cleanup(); }
            Console.WriteLine("Disposing Win32Window (unmanaged)...");
            if (_hwnd != IntPtr.Zero) { Console.WriteLine($"Destroying window {_hwnd} during Dispose..."); NativeMethods.DestroyWindow(_hwnd); _hwnd = IntPtr.Zero; }
            else { if (_gcHandle.IsAllocated) { Console.WriteLine("Freeing dangling GCHandle..."); _gcHandle.Free(); } }
            _isDisposed = true; Console.WriteLine("Win32Window disposed.");
        }
    }
    ~Win32Window() { Console.WriteLine("Win32Window Finalizer!"); Dispose(false); }
}

// Ensure MouseButton enum exists
public enum MouseButton { Left, Right, Middle, XButton1, XButton2 }