using System;
using System.Numerics;
using DirectUI.Core;
using Vortice.Mathematics;

namespace DirectUI;

internal class InternalKnobLogic
{
    private int _id;
    private bool _isPressed;
    private Vector2 _dragStartPosition;
    private float _valueAtDragStart;

    public bool UpdateAndDraw(
        int id,
        ref float currentValue,
        float minValue,
        float maxValue,
        float radius,
        Vector2 position,
        KnobStyle? theme,
        bool disabled,
        float sensitivity = 0.005f)
    {
        _id = id;
        var context = UI.Context;
        var state = UI.State;
        var renderer = context.Renderer;
        var input = context.InputState;

        var themeId = HashCode.Combine(id, "theme");
        var finalTheme = theme ?? state.GetOrCreateElement<KnobStyle>(themeId);
        if (theme is null)
        {
            // Setup a default theme if none is provided
            finalTheme.BaseStyle.FillColor = DefaultTheme.NormalFill;
            finalTheme.BaseStyle.BorderColor = DefaultTheme.NormalBorder;
            finalTheme.BaseStyle.Roundness = 1.0f; // Circle
            finalTheme.BaseStyle.BorderLength = 1.5f;
            finalTheme.IndicatorColor = DefaultTheme.Accent;
            finalTheme.IndicatorThickness = Math.Max(1f, radius * 0.15f);
        }

        bool valueChanged = false;
        Vector2 center = position + new Vector2(radius, radius);
        Rect bounds = new(position.X, position.Y, radius * 2, radius * 2);

        bool isHovering = !disabled && bounds.Contains(input.MousePosition);
        _isPressed = state.ActivelyPressedElementId == _id;

        if (isHovering)
        {
            state.SetPotentialInputTarget(_id);
        }

        if (_isPressed && !input.IsLeftMouseDown)
        {
            state.ClearActivePress(_id);
            _isPressed = false;
        }

        if (!disabled && isHovering && input.WasLeftMousePressedThisFrame && state.PotentialInputTargetId == _id)
        {
            if (state.TrySetActivePress(_id, 1))
            {
                state.SetFocus(_id);
                _isPressed = true;
                _dragStartPosition = input.MousePosition;
                _valueAtDragStart = currentValue;
            }
        }

        if (_isPressed)
        {
            float deltaY = _dragStartPosition.Y - input.MousePosition.Y; // Inverted for natural feel
            float valueRange = maxValue - minValue;
            float change = deltaY * valueRange * sensitivity;

            float newValue = Math.Clamp(_valueAtDragStart + change, minValue, maxValue);
            if (Math.Abs(newValue - currentValue) > float.Epsilon)
            {
                currentValue = newValue;
                valueChanged = true;
            }
        }

        // --- Drawing ---
        // Base
        renderer.DrawBox(bounds, finalTheme.BaseStyle);

        // Indicator
        float normalizedValue = (currentValue - minValue) / (maxValue - minValue);
        float angleRangeRad = finalTheme.AngleRangeDegrees * (float)Math.PI / 180f;
        float startAngleRad = finalTheme.StartAngleDegrees * (float)Math.PI / 180f;
        float currentAngleRad = startAngleRad + (normalizedValue * angleRangeRad);

        Vector2 indicatorStart = center;
        Vector2 indicatorEnd = new(
            center.X + (float)Math.Cos(currentAngleRad) * radius * 0.8f,
            center.Y + (float)Math.Sin(currentAngleRad) * radius * 0.8f
        );

        renderer.DrawLine(indicatorStart, indicatorEnd, finalTheme.IndicatorColor, finalTheme.IndicatorThickness);

        return valueChanged;
    }
}