using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    public static float HSlider(
        int id,
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

        Vector2 drawPos = Context.Layout.ApplyLayout(position);

        // Culling Check
        Rect widgetBounds = new Rect(drawPos.X, drawPos.Y, size.X, size.Y);
        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(size);
            return currentValue;
        }

        InternalHSliderLogic sliderInstance = State.GetOrCreateElement<InternalHSliderLogic>(id);
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


        float newValue = sliderInstance.UpdateAndDraw(id, currentValue);
        Context.Layout.AdvanceLayout(sliderInstance.Size);
        return newValue;
    }

    public static float VSlider(
        int id,
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

        Vector2 drawPos = Context.Layout.ApplyLayout(position);

        // Culling Check
        Rect widgetBounds = new Rect(drawPos.X, drawPos.Y, size.X, size.Y);
        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(size);
            return currentValue;
        }

        InternalVSliderLogic sliderInstance = State.GetOrCreateElement<InternalVSliderLogic>(id);
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

        float newValue = sliderInstance.UpdateAndDraw(id, currentValue);
        Context.Layout.AdvanceLayout(sliderInstance.Size);
        return newValue;
    }
}