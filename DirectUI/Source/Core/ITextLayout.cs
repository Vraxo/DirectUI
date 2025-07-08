// DirectUI/Core/ITextLayout.cs
using System;
using System.Numerics;

namespace DirectUI.Core;

/// <summary>
/// Represents a block of text that has been fully processed for layout.
/// This is a backend-agnostic interface.
/// </summary>
public interface ITextLayout : IDisposable
{
    /// <summary>
    /// The final measured size of the laid-out text.
    /// </summary>
    Vector2 Size { get; }

    /// <summary>
    /// The original string content of this layout.
    /// </summary>
    string Text { get; }

    /// <summary>
    /// Retrieves the metrics for a glyph at a specific text position.
    /// </summary>
    /// <param name="textPosition">The zero-based index of the target character.</param>
    /// <param name="isTrailingHit">Indicates whether to measure the leading or trailing edge of the character.</param>
    /// <returns>Metrics describing the position and size of the specified character position.</returns>
    TextHitTestMetrics HitTestTextPosition(int textPosition, bool isTrailingHit);

    /// <summary>
    /// Performs a hit-test to determine which character position is at a given point.
    /// </summary>
    /// <param name="point">The point to test, relative to the layout's origin.</param>
    /// <returns>A result object containing the metrics and character position of the hit.</returns>
    TextHitTestResult HitTestPoint(Vector2 point);
}