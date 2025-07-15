// NativeMethods.cs
using System;
using System.Runtime.InteropServices;

// No changes needed here if it was correct from the previous step.
// Ensure these constants are defined:
// WM_NCCREATE, WM_CREATE, WM_NCDESTROY, WM_PAINT, WM_DESTROY, WM_SIZE,
// WM_CLOSE, WM_KEYDOWN, WM_LBUTTONDOWN, WM_LBUTTONUP, WM_MOUSEMOVE, WM_QUIT
// GWLP_USERDATA, VK_ESCAPE, etc.
// And the structs: WNDCLASSEX, MSG, POINT, RECT, CREATESTRUCT
// And the functions: RegisterClassEx, CreateWindowEx, GetMessage, etc.
// And the delegates: WndProc

namespace DirectUI;

public static class NativeMethods
{
    // --- Constants ---
    public const uint CS_HREDRAW = 0x0002;
    public const uint CS_VREDRAW = 0x0001;
    public const uint CS_OWNDC = 0x0020;

    public const uint WS_OVERLAPPED = 0x00000000;
    public const uint WS_CAPTION = 0x00C00000;
    public const uint WS_SYSMENU = 0x00080000;
    public const uint WS_THICKFRAME = 0x00040000;
    public const uint WS_MINIMIZEBOX = 0x00020000;
    public const uint WS_MAXIMIZEBOX = 0x00010000;
    public const uint WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
    public const uint WS_VISIBLE = 0x10000000;
    public const uint WS_POPUP = 0x80000000;

    public const int WM_NCCREATE = 0x0081;
    public const int WM_CREATE = 0x0001;
    public const int WM_NCDESTROY = 0x0082;
    public const int WM_PAINT = 0x000F;
    public const int WM_DESTROY = 0x0002;
    public const int WM_SIZE = 0x0005;
    public const int WM_CLOSE = 0x0010;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYUP = 0x0105;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202; // Ensure this exists
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_MBUTTONUP = 0x0208;
    public const int WM_XBUTTONDOWN = 0x020B;
    public const int WM_XBUTTONUP = 0x020C;
    public const int WM_MOUSEMOVE = 0x0200; // Ensure this exists
    public const int WM_MOUSEWHEEL = 0x020A;
    public const int WM_QUIT = 0x0012;
    public const int WM_CHAR = 0x0102;


    public const int CW_USEDEFAULT = unchecked((int)0x80000000);
    public const int SW_SHOWNORMAL = 1;
    public const int GWLP_USERDATA = -21;

    public const uint PM_REMOVE = 0x0001;

    // --- Structures ---
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct WNDCLASSEX
    { /* ... as before ... */
        public int cbSize;
        public uint style;
        public WndProc lpfnWndProc; // Use delegate type directly
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    { /* ... as before ... */
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    { /* ... as before ... */
        public int X;
        public int Y;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    { /* ... as before ... */
        public int left;
        public int top;
        public int right;
        public int bottom;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct CREATESTRUCT
    { /* ... as before ... */
        public IntPtr lpCreateParams;
        public IntPtr hInstance;
        public IntPtr hMenu;
        public IntPtr hwndParent;
        public int cy; // Height
        public int cx; // Width
        public int y;  // Top
        public int x;  // Left
        public int style;
        [MarshalAs(UnmanagedType.LPWStr)] // Assuming Unicode
        public string lpszName;
        [MarshalAs(UnmanagedType.LPWStr)] // Assuming Unicode
        public string lpszClass;
        public int dwExStyle;
    }

    // --- Delegates ---
    public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // --- Functions ---
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UpdateWindow(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern void PostQuitMessage(int nExitCode);
    [DllImport("user32.dll")]
    public static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
    [DllImport("user32.dll")]
    public static extern bool TranslateMessage([In] ref MSG lpMsg);
    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage([In] ref MSG lpmsg);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] // Add return type annotation
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam); // Added
    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);
    [DllImport("user32.dll")]
    public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
    [DllImport("user32.dll")]
    public static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetCapture(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ReleaseCapture();
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnableWindow(IntPtr hWnd, bool bEnable);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);


    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8) return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        else return SetWindowLong32(hWnd, nIndex, dwNewLong);
    }
    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8) return GetWindowLongPtr64(hWnd, nIndex);
        else return GetWindowLong32(hWnd, nIndex);
    }

    // --- Helpers ---
    public static short LoWord(IntPtr val) => unchecked((short)(long)val);
    public static short HiWord(IntPtr val) => unchecked((short)((long)val >> 16));
}