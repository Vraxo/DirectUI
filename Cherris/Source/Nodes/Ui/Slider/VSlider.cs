using Vortice.Mathematics;
namespace Cherris;

public class VSlider : Slider
{
    public VSliderDirection Direction { get; set; } = VSliderDirection.TopToBottom;

    public VSlider()
    {
        Size = new(16, 200);
        GrabberSize = new(24, 12);
    }
    protected override void CalculateTrackBounds()
    {
        Vector2 sliderVisualTopLeft = GlobalPosition - Origin;
        this.trackPosition = sliderVisualTopLeft;        trackMin = sliderVisualTopLeft.Y;        trackMax = sliderVisualTopLeft.Y + Size.Y;    }

    protected override void UpdateHoverStates()
    {
        Vector2 mousePos = GetLocalMousePosition();        Vector2 visualSliderTopLeft = this.trackPosition;
        trackHovered = mousePos.X >= visualSliderTopLeft.X &&
                       mousePos.X <= visualSliderTopLeft.X + Size.X &&
                       mousePos.Y >= visualSliderTopLeft.Y &&
                       mousePos.Y <= visualSliderTopLeft.Y + Size.Y;

        Vector2 grabberTopLeftPos = CalculateGrabberPosition();
        grabberHovered = mousePos.X >= grabberTopLeftPos.X &&
                         mousePos.X <= grabberTopLeftPos.X + GrabberSize.X &&
                         mousePos.Y >= grabberTopLeftPos.Y &&
                         mousePos.Y <= grabberTopLeftPos.Y + GrabberSize.Y;
    }

    protected override void HandleInput()
    {
        HandleMousePress();
        HandleMouseDrag();
        HandleMouseWheel();
    }

    private void HandleMousePress()
    {
        if (Input.IsMouseButtonPressed(MouseButtonCode.Left))
        {
            if (trackHovered)
            {
                float localMouseY = GetLocalMousePosition().Y;
                float clampedMouseY = Math.Clamp(localMouseY, this.trackPosition.Y, this.trackPosition.Y + Size.Y);
                Value = ConvertPositionToValue(clampedMouseY);
                grabberPressed = true;
                PlaySound();
            }
        }
        else if (Input.IsMouseButtonReleased(MouseButtonCode.Left))
        {
            grabberPressed = false;
        }
    }

    private void HandleMouseDrag()
    {
        if (!grabberPressed) return;

        if (Input.IsMouseButtonReleased(MouseButtonCode.Left))
        {
            grabberPressed = false;
            return;
        }
        float localMouseY = GetLocalMousePosition().Y;
        float clampedMouseY = Math.Clamp(localMouseY, this.trackPosition.Y, this.trackPosition.Y + Size.Y);
        Value = ConvertPositionToValue(clampedMouseY);
    }

    private void HandleMouseWheel()
    {
        if (!trackHovered && !grabberHovered) return;

        float wheelDelta = Input.GetMouseWheelMovement();
        if (wheelDelta == 0) return;

        Value = ApplyStep(Value + (wheelDelta * Step * (Direction == VSliderDirection.TopToBottom ? 1 : -1)));
        PlaySound();
    }

    protected override float ConvertPositionToValue(float positionOnTrackInWindowSpace)
    {
        float visualTrackTopEdge = this.trackPosition.Y;        float effectiveTrackHeight = Size.Y;

        if (effectiveTrackHeight <= 0) return MinValue;

        float normalized = (positionOnTrackInWindowSpace - visualTrackTopEdge) / effectiveTrackHeight;
        normalized = Math.Clamp(normalized, 0f, 1f);

        if (Direction == VSliderDirection.BottomToTop)
        {
            normalized = 1f - normalized;
        }

        float rawValue = MinValue + normalized * (MaxValue - MinValue);
        return ApplyStep(rawValue);
    }

    protected override void DrawForeground(DrawingContext context)
    {
        Vector2 sliderVisualTopLeft = GlobalPosition - Origin;
        float range = MaxValue - MinValue;
        float fillRatio = (range == 0) ? 0.0f : (this.Value - MinValue) / range;
        fillRatio = Math.Clamp(fillRatio, 0f, 1f);

        float foregroundHeight = Size.Y * fillRatio;
        Rect foregroundRect;

        if (Direction == VSliderDirection.BottomToTop)
        {
            foregroundRect = new Rect(
                sliderVisualTopLeft.X,
                sliderVisualTopLeft.Y + Size.Y - foregroundHeight,
                Size.X,
                foregroundHeight
            );
        }
        else        {
            foregroundRect = new Rect(
                sliderVisualTopLeft.X,
                sliderVisualTopLeft.Y,
                Size.X,
                foregroundHeight
            );
        }

        if (foregroundRect.Width > 0 && foregroundRect.Height > 0)
        {
            DrawStyledRectangle(context, foregroundRect, Style.Foreground);
        }
    }

    protected override Vector2 CalculateGrabberPosition()
    {
        Vector2 sliderVisualTopLeft = GlobalPosition - Origin;
        float range = MaxValue - MinValue;
        float normalizedValue = (range == 0) ? 0.0f : (this.Value - MinValue) / range;
        normalizedValue = Math.Clamp(normalizedValue, 0f, 1f);

        if (Direction == VSliderDirection.BottomToTop)
        {
            normalizedValue = 1f - normalizedValue;
        }

        float grabberCenterY_relativeToSliderTop = normalizedValue * Size.Y;
        float grabberTopY_relativeToSliderTop = grabberCenterY_relativeToSliderTop - GrabberSize.Y / 2f;
        grabberTopY_relativeToSliderTop = Math.Clamp(grabberTopY_relativeToSliderTop, 0, Size.Y - GrabberSize.Y);
        float finalGrabberTopY_global = sliderVisualTopLeft.Y + grabberTopY_relativeToSliderTop;

        float grabberLeftX_relativeToSliderLeft = (Size.X / 2f) - GrabberSize.X / 2f;
        float finalGrabberLeftX_global = sliderVisualTopLeft.X + grabberLeftX_relativeToSliderLeft;

        return new Vector2(finalGrabberLeftX_global, finalGrabberTopY_global);
    }

    protected override void UpdateGrabberThemeVisuals()
    {
        if (Disabled)
        {
            Style.Grabber.Current = Style.Grabber.Disabled;
            return;
        }

        if (grabberPressed)
        {
            Style.Grabber.Current = Style.Grabber.Pressed;
        }
        else if (Focused)
        {
            Style.Grabber.Current = grabberHovered ? Style.Grabber.Hover : Style.Grabber.Focused;
        }
        else if (grabberHovered)
        {
            Style.Grabber.Current = Style.Grabber.Hover;
        }
        else
        {
            Style.Grabber.Current = Style.Grabber.Normal;
        }
    }

    protected override void HandleKeyboardNavigation()
    {
        if (Input.IsActionPressed("UiUp"))
        {
            Value = ApplyStep(Value + Step * (Direction == VSliderDirection.TopToBottom ? -1 : 1));
            PlaySound();
        }
        else if (Input.IsActionPressed("UiDown"))
        {
            Value = ApplyStep(Value - Step * (Direction == VSliderDirection.TopToBottom ? -1 : 1));
            PlaySound();
        }
    }
}