using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    private static float SliderPrimitive<T>(
        string id,
        float currentValue,
        float minValue,
        float maxValue,
        Vector2 size, // logical
        Vector2 position, // logical
        float step,
        SliderStyle? theme,
        ButtonStylePack? grabberTheme,
        Vector2? grabberSize, // logical
        bool disabled,
        object? userData,
        Vector2? origin, // logical
        Action<T> configureSpecifics) where T : InternalSliderLogic, new()
    {
        if (!IsContextValid()) return currentValue;

        int intId = id.GetHashCode();
        var scale = Context.UIScale;
        var logicalSize = size;
        var physicalSize = logicalSize * scale;

        Vector2 drawPos = Context.Layout.ApplyLayout(position);

        // Automatic vertical position adjustment for HBox alignment
        if (Context.Layout.IsInLayoutContainer() && Context.Layout.PeekContainer() is HBoxContainerState hbox)
        {
            if (hbox.VerticalAlignment != VAlignment.Top && hbox.FixedRowHeight.HasValue)
            {
                float yOffset = 0;
                switch (hbox.VerticalAlignment)
                {
                    case VAlignment.Center:
                        yOffset = (hbox.FixedRowHeight.Value - logicalSize.Y) / 2f;
                        break;
                    case VAlignment.Bottom:
                        yOffset = hbox.FixedRowHeight.Value - logicalSize.Y;
                        break;
                }
                drawPos.Y += yOffset * scale;
            }
        }

        // Culling Check using physical units
        Rect widgetBounds = new Rect(drawPos.X, drawPos.Y, physicalSize.X, physicalSize.Y);
        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(logicalSize);
            return currentValue;
        }

        T sliderInstance = State.GetOrCreateElement<T>(intId);

        // Configure instance with PHYSICAL units
        sliderInstance.Position = drawPos;
        sliderInstance.Size = physicalSize;
        sliderInstance.GrabberSize = (grabberSize ?? new Vector2(16, 16)) * scale;
        sliderInstance.Origin = (origin ?? Vector2.Zero) * scale;
        sliderInstance.MinValue = minValue;
        sliderInstance.MaxValue = maxValue;
        sliderInstance.Step = step;
        sliderInstance.Theme = theme ?? sliderInstance.Theme ?? new SliderStyle();
        sliderInstance.GrabberTheme = grabberTheme ?? sliderInstance.GrabberTheme ?? new ButtonStylePack();
        sliderInstance.Disabled = disabled;
        sliderInstance.UserData = userData;

        // Configure slider-specific properties (H or V)
        configureSpecifics(sliderInstance);

        float newValue = sliderInstance.UpdateAndDraw(intId, currentValue);
        Context.Layout.AdvanceLayout(logicalSize);
        return newValue;
    }

    public static float HSlider(
        string id,
        float currentValue,
        float minValue,
        float maxValue,
        Vector2 size,
        Vector2 position = default,
        float step = 0.01f,
        SliderStyle? theme = null,
        ButtonStylePack? grabberTheme = null,
        Vector2? grabberSize = null,
        HSliderDirection direction = HSliderDirection.LeftToRight,
        bool disabled = false,
        object? userData = null,
        Vector2? origin = null)
    {
        return SliderPrimitive<InternalHSliderLogic>(
            id, currentValue, minValue, maxValue, size, position, step,
            theme, grabberTheme, grabberSize, disabled, userData, origin,
            sliderInstance => sliderInstance.Direction = direction
        );
    }

    public static float VSlider(
        string id,
        float currentValue,
        float minValue,
        float maxValue,
        Vector2 size,
        Vector2 position = default,
        float step = 0.01f,
        SliderStyle? theme = null,
        ButtonStylePack? grabberTheme = null,
        Vector2? grabberSize = null,
        VSliderDirection direction = VSliderDirection.TopToBottom,
        bool disabled = false,
        object? userData = null,
        Vector2? origin = null)
    {
        return SliderPrimitive<InternalVSliderLogic>(
            id, currentValue, minValue, maxValue, size, position, step,
            theme, grabberTheme, grabberSize, disabled, userData, origin,
            sliderInstance => sliderInstance.Direction = direction
        );
    }
}