using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    /// <summary>
    /// Draws a circular knob control. Value is changed by clicking and dragging vertically.
    /// </summary>
    /// <param name="id">A unique identifier for the knob.</param>
    /// <param name="currentValue">The current value, passed by ref to be modified.</param>
    /// <param name="minValue">The minimum value of the knob.</param>
    /// <param name="maxValue">The maximum value of the knob.</param>
    /// <param name="radius">The radius of the knob.</param>
    /// <param name="theme">The style of the knob.</param>
    /// <param name="disabled">Whether the knob is disabled.</param>
    /// <param name="sensitivity">Controls how much the value changes per pixel of vertical mouse movement.</param>
    /// <returns>True if the value was changed this frame, otherwise false.</returns>
    public static bool Knob(
        string id,
        ref float currentValue,
        float minValue,
        float maxValue,
        float radius = 24f,
        KnobStyle? theme = null,
        bool disabled = false,
        float sensitivity = 0.005f)
    {
        if (!IsContextValid()) return false;

        int intId = id.GetHashCode();
        var knobInstance = State.GetOrCreateElement<InternalKnobLogic>(intId);
        
        Vector2 size = new(radius * 2, radius * 2);
        Vector2 drawPos = Context.Layout.GetCurrentPosition();
        Rect widgetBounds = new(drawPos.X, drawPos.Y, size.X, size.Y);

        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(size);
            return false;
        }

        bool valueChanged = knobInstance.UpdateAndDraw(
            intId,
            ref currentValue,
            minValue,
            maxValue,
            radius,
            drawPos,
            theme,
            disabled,
            sensitivity);

        Context.Layout.AdvanceLayout(size);
        return valueChanged;
    }
}
