using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public static partial class UI
{
    // --- Widgets ---
    public static bool Button(string id, ButtonDefinition definition)
    {
        if (!IsContextValid() || definition is null) return false;
        Button buttonInstance = GetOrCreateElement<Button>(id);
        Vector2 elementPosition = GetCurrentLayoutPositionInternal();
        buttonInstance.Position = elementPosition;
        ApplyButtonDefinition(buttonInstance, definition);

        bool pushedClip = false;
        Rect cellClipRect = Rect.Empty;
        if (IsInLayoutContainer())
        {
            if (containerStack.Peek() is GridContainerState grid)
            {
                float clipStartY = grid.CurrentDrawPosition.Y; float gridBottomY = grid.StartPosition.Y + grid.AvailableSize.Y;
                float clipHeight = Math.Max(0f, gridBottomY - clipStartY);
                cellClipRect = new Rect(grid.CurrentDrawPosition.X, clipStartY, Math.Max(0f, grid.CellWidth), clipHeight);
                if (CurrentRenderTarget is not null && cellClipRect.Width > 0 && cellClipRect.Height > 0)
                { CurrentRenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased); pushedClip = true; }
            }
        }

        bool clicked = buttonInstance.Update(id);
        if (pushedClip && CurrentRenderTarget is not null)
        { CurrentRenderTarget.PopAxisAlignedClip(); }

        AdvanceLayout(buttonInstance.Size);
        return clicked;
    }

    public static float HSlider(string id, float currentValue, SliderDefinition definition)
    {
        if (!IsContextValid() || definition is null) return currentValue;
        InternalHSliderLogic sliderInstance = GetOrCreateElement<InternalHSliderLogic>(id);
        Vector2 elementPosition = GetCurrentLayoutPositionInternal();
        sliderInstance.Position = elementPosition;
        ApplySliderDefinition(sliderInstance, definition);
        sliderInstance.Direction = definition.HorizontalDirection;

        bool pushedClip = false;
        Rect cellClipRect = Rect.Empty;
        if (IsInLayoutContainer())
        {
            if (containerStack.Peek() is GridContainerState grid)
            {
                float clipStartY = grid.CurrentDrawPosition.Y; float gridBottomY = grid.StartPosition.Y + grid.AvailableSize.Y;
                float clipHeight = Math.Max(0f, gridBottomY - clipStartY);
                cellClipRect = new Rect(grid.CurrentDrawPosition.X, clipStartY, Math.Max(0f, grid.CellWidth), clipHeight);
                if (CurrentRenderTarget is not null && cellClipRect.Width > 0 && cellClipRect.Height > 0)
                { CurrentRenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased); pushedClip = true; }
            }
        }

        float newValue = sliderInstance.UpdateAndDraw(id, CurrentInputState, GetCurrentDrawingContext(), currentValue);
        if (pushedClip && CurrentRenderTarget is not null)
        { CurrentRenderTarget.PopAxisAlignedClip(); }

        AdvanceLayout(sliderInstance.Size);
        return newValue;
    }

    public static float VSlider(string id, float currentValue, SliderDefinition definition)
    {
        if (!IsContextValid() || definition is null) return currentValue;
        InternalVSliderLogic sliderInstance = GetOrCreateElement<InternalVSliderLogic>(id);
        Vector2 elementPosition = GetCurrentLayoutPositionInternal();
        sliderInstance.Position = elementPosition;
        ApplySliderDefinition(sliderInstance, definition);
        sliderInstance.Direction = definition.VerticalDirection;

        bool pushedClip = false;
        Rect cellClipRect = Rect.Empty;
        if (IsInLayoutContainer())
        {
            if (containerStack.Peek() is GridContainerState grid)
            {
                float clipStartY = grid.CurrentDrawPosition.Y; float gridBottomY = grid.StartPosition.Y + grid.AvailableSize.Y;
                float clipHeight = Math.Max(0f, gridBottomY - clipStartY);
                cellClipRect = new Rect(grid.CurrentDrawPosition.X, clipStartY, Math.Max(0f, grid.CellWidth), clipHeight);
                if (CurrentRenderTarget is not null && cellClipRect.Width > 0 && cellClipRect.Height > 0)
                { CurrentRenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased); pushedClip = true; }
            }
        }

        float newValue = sliderInstance.UpdateAndDraw(id, CurrentInputState, GetCurrentDrawingContext(), currentValue);
        if (pushedClip && CurrentRenderTarget is not null)
        { CurrentRenderTarget.PopAxisAlignedClip(); }

        AdvanceLayout(sliderInstance.Size);

        return newValue;
    }
}