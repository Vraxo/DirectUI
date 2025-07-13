// DirectUI/Core/ITextService.cs
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI.Core;

/// <summary>
/// Defines the abstract contract for text processing services like measurement and layout.
/// The UI library uses this to handle all font-related operations.
/// </summary>
public interface ITextService
{
    /// <summary>
    /// Measures the bounding box of a string given a specific style, without layout constraints.
    /// </summary>
    Vector2 MeasureText(string text, ButtonStyle style);

    /// <summary>
    /// Creates or retrieves a cached, fully formatted text layout object.
    /// </summary>
    ITextLayout GetTextLayout(string text, ButtonStyle style, Vector2 maxSize, Alignment alignment);

    /// <summary>
    /// Cleans up any cached resources managed by the text service.
    /// </summary>
    void Cleanup();
}