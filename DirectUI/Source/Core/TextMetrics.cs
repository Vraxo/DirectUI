// DirectUI/Core/TextMetrics.cs
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI.Core;

/// <summary>
/// Describes the metrics of a character position within a text layout.
/// This is a backend-agnostic struct.
/// </summary>
public readonly record struct TextHitTestMetrics(
    /// <summary>
    /// The top-left corner of the character position, relative to the layout's origin.
    /// </summary>
    Vector2 Point,

    /// <summary>
    /// The measured size of the character position.
    /// </summary>
    Vector2 Size
);

/// <summary>
/// The result of a point-based hit-test on a text layout.
/// This is a backend-agnostic struct.
/// </summary>
public readonly record struct TextHitTestResult(
    /// <summary>
    /// The character position that was hit.
    /// </summary>
    int TextPosition,

    /// <summary>
    /// A value indicating whether the hit occurred on the leading or trailing edge of the character.
    /// </summary>
    bool IsTrailingHit,

    /// <summary>
    /// A value indicating whether the hit occurred inside the text string.
    /// </summary>
    bool IsInside,

    /// <summary>
    /// The detailed metrics of the hit character position.
    /// </summary>
    TextHitTestMetrics Metrics
);