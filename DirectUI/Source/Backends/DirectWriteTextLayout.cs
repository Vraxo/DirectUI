// DirectUI/Backends/DirectWriteTextLayout.cs
using System;
using System.Numerics;
using DirectUI.Core;
using Vortice.DirectWrite;
using SharpGen.Runtime;

namespace DirectUI.Backends;

/// <summary>
/// A DirectWrite-specific implementation of the ITextLayout interface.
/// This class wraps an IDWriteTextLayout object. It is internal to the backend.
/// </summary>
internal class DirectWriteTextLayout : ITextLayout
{
    public IDWriteTextLayout DWriteLayout { get; }

    public Vector2 Size { get; }
    public string Text { get; }

    public DirectWriteTextLayout(IDWriteTextLayout dwriteLayout, string text) // Added 'string text' parameter
    {
        DWriteLayout = dwriteLayout;
        Size = new Vector2(dwriteLayout.Metrics.WidthIncludingTrailingWhitespace, dwriteLayout.Metrics.Height);
        Text = text; // Assign the provided text
    }

    public TextHitTestMetrics HitTestTextPosition(int textPosition, bool isTrailingHit)
    {
        DWriteLayout.HitTestTextPosition((uint)textPosition, isTrailingHit, out float x, out float y, out var metrics);
        return new TextHitTestMetrics(new Vector2(x, y), new Vector2(metrics.Width, metrics.Height));
    }

    public TextHitTestResult HitTestPoint(Vector2 point)
    {
        DWriteLayout.HitTestPoint(point.X, point.Y, out RawBool isTrailingHit, out RawBool isInside, out var hitTestMetrics);

        var metrics = new TextHitTestMetrics(
            new Vector2(hitTestMetrics.Left, hitTestMetrics.Top),
            new Vector2(hitTestMetrics.Width, hitTestMetrics.Height)
        );

        return new TextHitTestResult(
            (int)hitTestMetrics.TextPosition,
            isTrailingHit,
            isInside,
            metrics
        );
    }

    public void Dispose()
    {
        DWriteLayout.Dispose();
        GC.SuppressFinalize(this);
    }

    ~DirectWriteTextLayout()
    {
        Dispose();
    }
}