// Summary: Changed the press handling logic to call the new 'UI.SetButtonPotentialCaptorForFrame' method.
using System;
using System.Numerics;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Vortice.Direct2D1;
using SharpGen.Runtime;

namespace DirectUI;

public class Button
{
    public enum ActionMode { Release, Press }
    public enum ClickBehavior { Left, Right, Both }

    // --- Properties ---
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
    public ClickBehavior Behavior { get; set; } = Button.ClickBehavior.Left;
    public bool Disabled { get; set; } = false;
    public object? UserData { get; set; } = null;

    // Internal state managed locally
    public bool IsHovering { get; internal set; } = false;
    private bool isPressed = false;

    public Rect GlobalBounds
    {
        get
        {
            float x = Position.X - Origin.X;
            float y = Position.Y - Origin.Y;
            float width = Math.Max(0, Size.X);
            float height = Math.Max(0, Size.Y);
            var calculatedBounds = new Rect(x, y, width, height);
            return calculatedBounds;
        }
    }

    // --- Main Update Method ---
    internal bool Update(string id)
    {
        ID2D1HwndRenderTarget? renderTarget = UI.CurrentRenderTarget;
        IDWriteFactory? dwriteFactory = UI.CurrentDWriteFactory;
        InputState input = UI.CurrentInputState;

        if (renderTarget is null || dwriteFactory is null) return false;

        bool wasClickedThisFrame = false;
        isPressed = false;

        if (Disabled)
        {
            IsHovering = false;
            if (UI.ActivelyPressedElementId == id) UI.ClearActivePress(id);
        }
        else
        {
            // 1. Check Hover State & Set Potential Target
            Rect bounds = GlobalBounds;
            IsHovering = bounds.Width > 0 && bounds.Height > 0 && bounds.Contains(input.MousePosition.X, input.MousePosition.Y);
            if (IsHovering)
            {
                UI.SetPotentialInputTarget(id);
            }

            // Relevant Input States
            bool primaryActionHeld = (Behavior is ClickBehavior.Left or ClickBehavior.Both) && input.IsLeftMouseDown;
            bool primaryActionPressedThisFrame = (Behavior is ClickBehavior.Left or ClickBehavior.Both) && input.WasLeftMousePressedThisFrame;

            // 2. Handle Mouse Release Logic
            if (!primaryActionHeld && UI.ActivelyPressedElementId == id)
            {
                if (IsHovering && LeftClickActionMode is ActionMode.Release)
                {
                    wasClickedThisFrame = true;
                }
                UI.ClearActivePress(id);
            }

            // 3. Handle Mouse Press Attempt (Overwriting Logic)
            if (primaryActionPressedThisFrame)
            {
                // Check if hovered, is potential target, and no drag was already active.
                if (IsHovering && UI.PotentialInputTargetId == id && !UI.dragInProgressFromPreviousFrame)
                {
                    // Use the dedicated button method to claim potential capture AND set the flag.
                    UI.SetButtonPotentialCaptorForFrame(id);
                }
            }

            // 4. Determine CURRENT Visual Pressed State
            // Visual state depends on whether this button is the globally active element.
            isPressed = (UI.ActivelyPressedElementId == id);

        } // End !Disabled block

        // --- Update Style & Size ---
        UpdateStyle();
        PerformAutoWidth(dwriteFactory);

        // --- Drawing ---
        try
        {
            if (GlobalBounds.Width > 0 && GlobalBounds.Height > 0)
            {
                DrawBackground(renderTarget);
                DrawText(renderTarget, dwriteFactory);
            }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        { Console.WriteLine($"Render target needs recreation: {ex.Message}"); UI.CleanupResources(); return false; }
        catch (Exception ex)
        { Console.WriteLine($"Error drawing button: {ex}"); return false; }


        // --- Final Click Determination ---
        // Click happens if mode is Press AND this button was the final captor for the frame.
        if (!wasClickedThisFrame && LeftClickActionMode is ActionMode.Press && UI.InputCaptorId == id)
        {
            wasClickedThisFrame = true;
        }

        return wasClickedThisFrame;
    }


    // --- Internal Logic Methods ---
    internal void UpdateStyle()
    {
        Themes?.UpdateCurrentStyle(IsHovering, isPressed, Disabled);
    }

    internal Vector2 MeasureText(IDWriteFactory dwriteFactory)
    {
        if (string.IsNullOrEmpty(Text) || Themes?.Current?.FontName is null || dwriteFactory is null) return Vector2.Zero;
        IDWriteTextFormat? textFormat = null;
        try
        {
            ButtonStyle style = Themes.Current;
            textFormat = dwriteFactory.CreateTextFormat(style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, "en-us");
            if (textFormat is null) { Console.WriteLine("Warning: Failed to create TextFormat for measurement."); return Vector2.Zero; }
            using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(Text, textFormat, float.MaxValue, float.MaxValue);
            TextMetrics textMetrics = textLayout.Metrics;
            return new Vector2(textMetrics.WidthIncludingTrailingWhitespace, textMetrics.Height);
        }
        catch (Exception ex) { Console.WriteLine($"Error measuring text: {ex.Message}"); return Vector2.Zero; }
        finally { textFormat?.Dispose(); }
    }

    internal void PerformAutoWidth(IDWriteFactory dwriteFactory)
    {
        if (!AutoWidth || dwriteFactory is null) return;
        Vector2 textSize = MeasureText(dwriteFactory); float desiredWidth = textSize.X + TextMargin.X * 2;
        if (desiredWidth > 0 && Math.Abs(Size.X - desiredWidth) > 0.1f) { Size = new Vector2(desiredWidth, Size.Y); }
        else if (desiredWidth <= 0 && Size.X != 0) { /* Optional: Reset size or set MinWidth */ }
    }

    // --- Drawing Methods ---
    private void DrawBackground(ID2D1RenderTarget renderTarget)
    {
        Rect bounds = GlobalBounds; ButtonStyle? style = Themes?.Current;
        if (style is null || bounds.Width <= 0 || bounds.Height <= 0) return;
        UI.DrawBoxStyleHelper(renderTarget, new Vector2(bounds.X, bounds.Y), new Vector2(bounds.Width, bounds.Height), style);
    }

    private void DrawText(ID2D1RenderTarget renderTarget, IDWriteFactory dwriteFactory)
    {
        if (string.IsNullOrEmpty(Text)) return; ButtonStyle? style = Themes?.Current; if (style is null) return;
        ID2D1SolidColorBrush textBrush = UI.GetOrCreateBrush(style.FontColor); if (textBrush is null) return;
        IDWriteTextFormat? textFormat = null;
        try
        {
            textFormat = dwriteFactory.CreateTextFormat(style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, "en-us");
            if (textFormat is null) return;
            textFormat.TextAlignment = TextAlignment.Horizontal switch { HAlignment.Left => Vortice.DirectWrite.TextAlignment.Leading, HAlignment.Center => Vortice.DirectWrite.TextAlignment.Center, HAlignment.Right => Vortice.DirectWrite.TextAlignment.Trailing, _ => Vortice.DirectWrite.TextAlignment.Leading };
            textFormat.ParagraphAlignment = TextAlignment.Vertical switch { VAlignment.Top => Vortice.DirectWrite.ParagraphAlignment.Near, VAlignment.Center => Vortice.DirectWrite.ParagraphAlignment.Center, VAlignment.Bottom => Vortice.DirectWrite.ParagraphAlignment.Far, _ => Vortice.DirectWrite.ParagraphAlignment.Near };
            Rect bounds = GlobalBounds; if (bounds.Width <= 0 || bounds.Height <= 0) return;
            Rect layoutRect = bounds; layoutRect.Left += TextOffset.X; layoutRect.Top += TextOffset.Y;
            renderTarget.DrawText(Text, textFormat, layoutRect, textBrush, DrawTextOptions.Clip);
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code) { Console.WriteLine($"Text Draw failed (RecreateTarget): {ex.Message}."); textFormat = null; }
        catch (Exception ex) { Console.WriteLine($"Error drawing button text: {ex.Message}"); }
        finally { textFormat?.Dispose(); }
    }
}