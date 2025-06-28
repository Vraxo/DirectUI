using System.Numerics;

namespace DirectUI;

public static partial class UI
{
    public static float HSlider(string id, float currentValue, SliderDefinition definition)
    {
        if (!IsContextValid() || definition is null) return currentValue;
        InternalHSliderLogic sliderInstance = State.GetOrCreateElement<InternalHSliderLogic>(id);
        sliderInstance.Position = Context.Layout.ApplyLayout(definition.Position);
        ApplySliderDefinition(sliderInstance, definition);
        sliderInstance.Direction = definition.HorizontalDirection;

        float newValue = sliderInstance.UpdateAndDraw(id, currentValue);
        Context.Layout.AdvanceLayout(sliderInstance.Size);
        return newValue;
    }

    public static float VSlider(string id, float currentValue, SliderDefinition definition)
    {
        if (!IsContextValid() || definition is null) return currentValue;
        InternalVSliderLogic sliderInstance = State.GetOrCreateElement<InternalVSliderLogic>(id);
        sliderInstance.Position = Context.Layout.ApplyLayout(definition.Position);
        ApplySliderDefinition(sliderInstance, definition);
        sliderInstance.Direction = definition.VerticalDirection;

        float newValue = sliderInstance.UpdateAndDraw(id, currentValue);
        Context.Layout.AdvanceLayout(sliderInstance.Size);
        return newValue;
    }
}