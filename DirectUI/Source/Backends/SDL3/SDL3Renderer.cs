using System;
using System.Collections.Generic;
using System.Numerics;
using DirectUI.Core;
using SDL3;
using Veldrid.Sdl2;
using Vortice.Direct2D1; // For AntialiasMode enum, even if not used by SDL
using Vortice.Mathematics;
using Vortice.DirectWrite; // For FontWeight, etc.

namespace DirectUI.Backends.SDL3;

public unsafe class SDL3Renderer : IRenderer
{
    private readonly nint _gpuDevice;
    private readonly nint _windowPtr;

    private int _windowWidth;
    private int _windowHeight;

    public Vector2 RenderTargetSize
    {
        get
        {
            SDL.GetWindowSize(_windowPtr, out _windowWidth, out _windowHeight);
            return new(_windowWidth, _windowHeight);
        }
    }

    public SDL3Renderer(nint gpuDevice, nint windowPtr)
    {
        _gpuDevice = gpuDevice;
        _windowPtr = windowPtr;

        SDL.GetWindowSize(_windowPtr, out _windowWidth, out _windowHeight);
    }

    internal void UpdateWindowSize(int width, int height)
    {
        _windowWidth = width;
        _windowHeight = height;
    }

    public void DrawLine(Vector2 p1, Vector2 p2, Drawing.Color color, float strokeWidth)
    {
        // No-op: 2D UI rendering is disabled for this backend to focus on the 3D cube.
    }

    public void DrawBox(Vortice.Mathematics.Rect rect, BoxStyle style)
    {
        // No-op: 2D UI rendering is disabled for this backend to focus on the 3D cube.
    }

    public void DrawText(Vector2 origin, string text, ButtonStyle style, Alignment alignment, Vector2 maxSize, Drawing.Color color)
    {
        // No-op: 2D UI rendering is disabled for this backend to focus on the 3D cube.
    }

    public void PushClipRect(Rect rect, AntialiasMode antialiasMode)
    {
        // No-op
    }

    public void PopClipRect()
    {
        // No-op
    }

    public void Flush()
    {
        // No-op
    }

    public void Cleanup()
    {
        // No resources are created in this version of the renderer.
    }
}