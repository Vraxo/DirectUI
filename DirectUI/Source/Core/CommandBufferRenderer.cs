using System;
using System.Collections.Generic;
using System.Numerics;
using DirectUI.Drawing;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace DirectUI.Core;

/// <summary>
/// An implementation of IRenderer that records drawing commands instead of executing them immediately.
/// This allows for a two-pass style of rendering within a single UI pass for specific widgets,
/// enabling dynamically sized backgrounds to be drawn before the content that determines their size.
/// </summary>
internal class CommandBufferRenderer : IRenderer
{
    private readonly IRenderer _passthroughRenderer;
    private readonly List<Action<IRenderer>> _commands = new();

    public CommandBufferRenderer(IRenderer passthroughRenderer)
    {
        _passthroughRenderer = passthroughRenderer;
    }

    public void Replay(IRenderer targetRenderer)
    {
        foreach (var command in _commands)
        {
            command(targetRenderer);
        }
    }

    // Pass-through properties and methods that don't involve drawing
    public Vector2 RenderTargetSize => _passthroughRenderer.RenderTargetSize;
    public void Flush() => _commands.Add(r => r.Flush()); // Record flush as well

    // Recording implementations of drawing methods
    public void DrawLine(Vector2 p1, Vector2 p2, Color color, float strokeWidth)
    {
        _commands.Add(r => r.DrawLine(p1, p2, color, strokeWidth));
    }

    public void DrawBox(Rect rect, BoxStyle style)
    {
        // Important: Create a copy of the style, as the original might be mutated
        // between the time the command is recorded and when it's replayed.
        var styleCopy = new BoxStyle(style);
        _commands.Add(r => r.DrawBox(rect, styleCopy));
    }

    public void DrawText(Vector2 origin, string text, ButtonStyle style, Alignment alignment, Vector2 maxSize, Color color)
    {
        // Important: Create a copy of the style.
        var styleCopy = new ButtonStyle(style);
        _commands.Add(r => r.DrawText(origin, text, styleCopy, alignment, maxSize, color));
    }

    public void DrawImage(byte[] imageData, string imageKey, Rect destination)
    {
        // Image data is large, so we assume it's immutable and don't copy it.
        _commands.Add(r => r.DrawImage(imageData, imageKey, destination));
    }

    public void PushClipRect(Rect rect, AntialiasMode antialiasMode)
    {
        _commands.Add(r => r.PushClipRect(rect, antialiasMode));
    }

    public void PopClipRect()
    {
        _commands.Add(r => r.PopClipRect());
    }
}