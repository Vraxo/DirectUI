using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
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
        if (!IsContextValid()) return currentValue;

        int intId = id.GetHashCode();
        var scale = Context.UIScale;
        var logicalSize = size;
        Vector2 drawPos = Context.Layout.ApplyLayout(position);

        // New: Automatically adjust vertical position for HBox alignment
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

        // Culling Check
        Rect widgetBounds = new Rect(drawPos.X, drawPos.Y, size.X, size.Y);
        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(size);
            return currentValue;
        }

        InternalHSliderLogic sliderInstance = State.GetOrCreateElement<InternalHSliderLogic>(intId);
        sliderInstance.Position = drawPos;

        // Configure instance
        sliderInstance.Size = size;
        sliderInstance.MinValue = minValue;
        sliderInstance.MaxValue = maxValue;
        sliderInstance.Step = step;
        sliderInstance.Theme = theme ?? sliderInstance.Theme ?? new SliderStyle();
        sliderInstance.GrabberTheme = grabberTheme ?? sliderInstance.GrabberTheme ?? new ButtonStylePack();
        sliderInstance.GrabberSize = grabberSize ?? new Vector2(16, 16);
        sliderInstance.Origin = origin ?? Vector2.Zero;
        sliderInstance.Disabled = disabled;
        sliderInstance.UserData = userData;
        sliderInstance.Direction = direction;


        float newValue = sliderInstance.UpdateAndDraw(intId, currentValue);
        Context.Layout.AdvanceLayout(sliderInstance.Size);
        return newValue;
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
        if (!IsContextValid()) return currentValue;

        int intId = id.GetHashCode();
        var scale = Context.UIScale;
        var logicalSize = size;
        Vector2 drawPos = Context.Layout.ApplyLayout(position);

        // New: Automatically adjust vertical position for HBox alignment
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

        // Culling Check
        Rect widgetBounds = new Rect(drawPos.X, drawPos.Y, size.X, size.Y);
        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(size);
            return currentValue;
        }

        InternalVSliderLogic sliderInstance = State.GetOrCreateElement<InternalVSliderLogic>(intId);
        sliderInstance.Position = drawPos;

        // Configure instance
        sliderInstance.Size = size;
        sliderInstance.MinValue = minValue;
        sliderInstance.MaxValue = maxValue;
        sliderInstance.Step = step;
        sliderInstance.Theme = theme ?? sliderInstance.Theme ?? new SliderStyle();
        sliderInstance.GrabberTheme = grabberTheme ?? sliderInstance.GrabberTheme ?? new ButtonStylePack();
        sliderInstance.GrabberSize = grabberSize ?? new Vector2(16, 16);
        sliderInstance.Origin = origin ?? Vector2.Zero;
        sliderInstance.Disabled = disabled;
        sliderInstance.UserData = userData;
        sliderInstance.Direction = direction;

        float newValue = sliderInstance.UpdateAndDraw(intId, currentValue);
        Context.Layout.AdvanceLayout(sliderInstance.Size);
        return newValue;
    }
}