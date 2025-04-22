// Button.cs
// Modified GlobalBounds getter to use Rect(x, y, width, height) constructor.
using System;
using System.Numerics;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Vortice.Direct2D1;
using SharpGen.Runtime;

namespace DirectUI;

public class Button
{
    public enum ActionMode
    {
        Release,
        Press
    }

    public enum ClickBehavior
    {
        Left,
        Right,
        Both
    }

    public Vector2 Position { get; set; } = Vector2.Zero;
    public Vector2 Size { get; set; } = new(84, 28);
    public Vector2 Origin { get; set; } = Vector2.Zero;
    public string Text { get; set; } = "";
    public Vector2 TextOffset { get; set; } = Vector2.Zero;
    public Alignment TextAlignment { get; set; } = new(HAlignment.Center, VAlignment.Center);
    public ButtonStylePack Themes { get; set; } = new();
    public ActionMode LeftClickActionMode { get; set; } = ActionMode.Release;
    public bool AutoWidth { get; set; } = false;
    public Vector2 TextMargin { get; set; } = new(10, 5);
    public ClickBehavior Behavior { get; set; } = ClickBehavior.Left;
    public bool Disabled { get; set; } = false;
    public object? UserData { get; set; } = null;

    public bool IsHovering { get; internal set; } = false;
    public bool IsPressed { get; internal set; } = false;

    public Rect GlobalBounds
    {
        get
        {
            Console.WriteLine($"  [GlobalBounds Getter] Position=({Position.X},{Position.Y}), Origin=({Origin.X},{Origin.Y}), Size=({Size.X},{Size.Y})");

            // Calculate components
            float posX = Position.X;
            float posY = Position.Y;
            float originX = Origin.X;
            float originY = Origin.Y;
            float sizeX = Size.X;
            float sizeY = Size.Y;

            float x = posX - originX; // This is 'left' or 'x'
            float y = posY - originY; // This is 'top' or 'y'
            float width = sizeX;      // Use size directly for width
            float height = sizeY;     // Use size directly for height

            Console.WriteLine($"  [GlobalBounds Getter] Using Constructor: Rect(x={x}, y={y}, width={width}, height={height})");

            // --- Use alternative Rect constructor ---
            var calculatedBounds = new Rect(x, y, width, height);
            // --- End alternative ---

            Console.WriteLine($"  [GlobalBounds Getter] Constructed=({calculatedBounds.Left}, {calculatedBounds.Top}, {calculatedBounds.Right}, {calculatedBounds.Bottom})");
            Console.WriteLine($"  [GlobalBounds Getter] Constructed Width={calculatedBounds.Width}, Height={calculatedBounds.Height}"); // Log Width/Height too

            return calculatedBounds;
        }
    }

    public event Action<Button>? Clicked;
    public event Action<Button>? MouseEntered;
    public event Action<Button>? MouseExited;

    public bool Update()
    {
        ID2D1HwndRenderTarget? renderTarget = UI.CurrentRenderTarget;
        IDWriteFactory? dwriteFactory = UI.CurrentDWriteFactory;
        InputState input = UI.CurrentInputState;

        if (renderTarget is null || dwriteFactory is null)
        {
            Console.WriteLine("Error: Button.Update called outside of UI.BeginFrame/EndFrame scope.");
            return false;
        }

        Console.WriteLine($"Button Update Start: Size=({Size.X}, {Size.Y})");

        Rect bounds = GlobalBounds;
        bool wasClickedThisFrame = false;
        bool previousHoverState = IsHovering;

        Console.WriteLine($"Button Update Bounds Check: Size=({Size.X}, {Size.Y}), Captured Bounds=({bounds.Left}, {bounds.Top}, {bounds.Right}, {bounds.Bottom})");


        if (Disabled)
        {
            IsHovering = false;
            IsPressed = false;
        }
        else
        {
            IsHovering = bounds.Contains(input.MousePosition.X, input.MousePosition.Y);

            if (Behavior is Button.ClickBehavior.Left or Button.ClickBehavior.Both)
            {
                if (IsHovering && input.WasLeftMousePressedThisFrame)
                {
                    IsPressed = true;
                    if (LeftClickActionMode is Button.ActionMode.Press)
                    {
                        InvokeClick();
                        wasClickedThisFrame = true;
                    }
                }
                else if (IsPressed && !input.IsLeftMouseDown)
                {
                    if (IsHovering && LeftClickActionMode is Button.ActionMode.Release)
                    {
                        InvokeClick();
                        wasClickedThisFrame = true;
                    }
                    IsPressed = false;
                }
                else if (!input.IsLeftMouseDown)
                {
                    IsPressed = false;
                }
            }
        }

        if (IsHovering && !previousHoverState)
        {
            InvokeMouseEnter();
        }
        else if (!IsHovering && previousHoverState)
        {
            InvokeMouseExit();
        }

        UpdateStyle();
        PerformAutoWidth(dwriteFactory);

        try
        {
            Console.WriteLine($"Button Draw Prep: Size=({Size.X}, {Size.Y})");
            Rect boundsForDraw = GlobalBounds;
            Console.WriteLine($"Button Draw Prep: Bounds For Draw=({boundsForDraw.Left}, {boundsForDraw.Top}, {boundsForDraw.Right}, {boundsForDraw.Bottom})");

            DrawBackground(renderTarget);
            DrawText(renderTarget, dwriteFactory);
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Render target needs recreation (Caught SharpGenException during Button drawing): {ex.Message}");
            UI.CleanupResources();
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled error during Button drawing: {ex}");
            UI.CleanupResources();
            return false;
        }

        return wasClickedThisFrame;
    }

    internal void InvokeClick()
    {
        Clicked?.Invoke(this);
    }

    internal void InvokeMouseEnter()
    {
        MouseEntered?.Invoke(this);
    }

    internal void InvokeMouseExit()
    {
        MouseExited?.Invoke(this);
    }

    internal void UpdateStyle()
    {
        Themes.UpdateCurrentStyle(IsHovering, IsPressed, Disabled);
    }

    internal Vector2 MeasureText(IDWriteFactory dwriteFactory)
    {
        if (string.IsNullOrEmpty(Text) || Themes?.Current?.FontName is null || dwriteFactory is null)
        {
            return Vector2.Zero;
        }

        IDWriteTextFormat? textFormat = null;
        try
        {
            ButtonStyle style = Themes.Current;
            textFormat = dwriteFactory.CreateTextFormat(
                style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, "en-us"
            );

            if (textFormat is null)
            {
                Console.WriteLine("Warning: Failed to create TextFormat for measurement.");
                return Vector2.Zero;
            }

            using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(Text, textFormat, float.MaxValue, float.MaxValue);
            TextMetrics textMetrics = textLayout.Metrics;
            return new Vector2(textMetrics.WidthIncludingTrailingWhitespace, textMetrics.Height);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error measuring text: {ex.Message}");
            return Vector2.Zero;
        }
        finally
        {
            textFormat?.Dispose();
        }
    }

    internal void PerformAutoWidth(IDWriteFactory dwriteFactory)
    {
        if (!AutoWidth)
        {
            return;
        }

        //Vector2 textSize = MeasureText(dwriteFactory);
        //float desiredWidth = textSize.X + TextMargin.X * 2;
        //if (desiredWidth > 0)
        //{
        //    Size = new Vector2(desiredWidth, Size.Y);
        //}
    }

    private void DrawBackground(ID2D1RenderTarget renderTarget)
    {
        Console.WriteLine($"  [DrawBackground Start] Size=({Size.X},{Size.Y})");
        Rect bounds = GlobalBounds;
        Console.WriteLine($"  [DrawBackground Start] Bounds=({bounds.Left}, {bounds.Top}, {bounds.Right}, {bounds.Bottom})");

        ButtonStyle style = Themes.Current;

        ID2D1SolidColorBrush fillBrush = UI.GetOrCreateBrush(style.FillColor);
        ID2D1SolidColorBrush borderBrush = UI.GetOrCreateBrush(style.BorderColor);

        bool canFill = style.FillColor.A > 0 && fillBrush is not null;
        bool canDrawBorder = style.BorderThickness > 0 && style.BorderColor.A > 0 && borderBrush is not null;

        if (style.Roundness > 0.0f && bounds.Width > 0 && bounds.Height > 0)
        {
            var radiusX = float.Max(0, bounds.Width * style.Roundness * 0.5f);
            var radiusY = float.Max(0, bounds.Height * style.Roundness * 0.5f);
            RoundedRectangle roundedRect = new((System.Drawing.RectangleF)bounds, radiusX, radiusY);

            if (canFill)
            {
                renderTarget.FillRoundedRectangle(roundedRect, fillBrush);
            }
            if (canDrawBorder)
            {
                renderTarget.DrawRoundedRectangle(roundedRect, borderBrush, style.BorderThickness);
            }
        }
        else
        {
            if (canFill)
            {
                renderTarget.FillRectangle(bounds, fillBrush);
            }
            if (canDrawBorder)
            {
                renderTarget.DrawRectangle(bounds, borderBrush, style.BorderThickness);
            }
        }
    }

    private void DrawText(ID2D1RenderTarget renderTarget, IDWriteFactory dwriteFactory)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        ButtonStyle style = Themes.Current;
        ID2D1SolidColorBrush textBrush = UI.GetOrCreateBrush(style.FontColor);
        if (textBrush is null)
        {
            return;
        }

        IDWriteTextFormat? textFormat = null;
        try
        {
            textFormat = dwriteFactory.CreateTextFormat(
                style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, "en-us"
            );

            if (textFormat is null)
            {
                Console.WriteLine("Error: Failed to create TextFormat for drawing.");
                return;
            }

            textFormat.TextAlignment = TextAlignment.Horizontal switch
            {
                HAlignment.Left => Vortice.DirectWrite.TextAlignment.Leading,
                HAlignment.Center => Vortice.DirectWrite.TextAlignment.Center,
                HAlignment.Right => Vortice.DirectWrite.TextAlignment.Trailing,
                _ => Vortice.DirectWrite.TextAlignment.Leading
            };
            textFormat.ParagraphAlignment = TextAlignment.Vertical switch
            {
                VAlignment.Top => Vortice.DirectWrite.ParagraphAlignment.Near,
                VAlignment.Center => Vortice.DirectWrite.ParagraphAlignment.Center,
                VAlignment.Bottom => Vortice.DirectWrite.ParagraphAlignment.Far,
                _ => Vortice.DirectWrite.ParagraphAlignment.Near
            };

            Rect bounds = GlobalBounds;
            Console.WriteLine($"  [DrawText Start] Bounds=({bounds.Left}, {bounds.Top}, {bounds.Right}, {bounds.Bottom})");

            Rect layoutRect = bounds;
            layoutRect.Left += TextOffset.X;
            layoutRect.Top += TextOffset.Y;

            renderTarget.DrawText(Text, textFormat, layoutRect, textBrush);
        }
        finally
        {
            textFormat?.Dispose();
        }
    }
}