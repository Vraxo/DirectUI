namespace DirectUI;

using System;
using System.Numerics;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Vortice.Direct2D1;
using SharpGen.Runtime;

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

    public bool IsHovering { get; internal set; } = false;
    public bool IsFocused { get; internal set; } = false;

    public Rect GlobalBounds
    {
        get
        {
            float x = Position.X - Origin.X;
            float y = Position.Y - Origin.Y;
            float width = Math.Max(0, Size.X);
            float height = Math.Max(0, Size.Y);
            Rect calculatedBounds = new(x, y, width, height);

            return calculatedBounds;
        }
    }

    private bool isPressed = false;


    internal bool Update(int intId)
    {
        var context = UI.Context;
        var state = UI.State;
        var resources = UI.Resources;

        var renderTarget = context.RenderTarget;
        var dwriteFactory = context.DWriteFactory;
        var input = context.InputState;

        if (renderTarget is null || dwriteFactory is null)
        {
            return false;
        }

        IsFocused = state.FocusedElementId == intId;

        PerformAutoWidth(dwriteFactory);

        bool wasClickedThisFrame = false;
        isPressed = false;

        if (Disabled)
        {
            IsHovering = false;
            if (state.ActivelyPressedElementId == intId)
            {
                state.ClearActivePress(intId);
            }
        }
        else
        {
            Rect bounds = GlobalBounds;
            IsHovering = bounds.Width > 0 && bounds.Height > 0 && bounds.Contains(input.MousePosition.X, input.MousePosition.Y);

            if (IsHovering)
            {
                state.SetPotentialInputTarget(intId);
            }

            bool primaryActionHeld = (Behavior is ClickBehavior.Left or ClickBehavior.Both) && input.IsLeftMouseDown;
            bool primaryActionPressedThisFrame = (Behavior is ClickBehavior.Left or ClickBehavior.Both) && input.WasLeftMousePressedThisFrame;

            if (!primaryActionHeld && state.ActivelyPressedElementId == intId)
            {
                if (IsHovering && LeftClickActionMode is ActionMode.Release)
                {
                    wasClickedThisFrame = true;
                }
                state.ClearActivePress(intId);
            }

            if (primaryActionPressedThisFrame)
            {
                if (IsHovering && state.PotentialInputTargetId == intId && !state.DragInProgressFromPreviousFrame)
                {
                    state.SetButtonPotentialCaptorForFrame(intId);
                    state.SetFocus(intId);
                }
            }
            isPressed = (state.ActivelyPressedElementId == intId);
        }

        UpdateStyle();

        try
        {
            Rect currentBounds = GlobalBounds;
            if (currentBounds.Width > 0 && currentBounds.Height > 0)
            {
                DrawBackground(renderTarget);
                DrawText(renderTarget, dwriteFactory);
            }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Render target needs recreation during Button draw: {ex.Message}");
            resources.CleanupResources();
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error drawing button {intId}: {ex}");
            return false;
        }

        if (!wasClickedThisFrame && LeftClickActionMode is ActionMode.Press && state.InputCaptorId == intId)
        {
            wasClickedThisFrame = true;
        }

        return wasClickedThisFrame;
    }

    internal void UpdateStyle()
    {
        Themes?.UpdateCurrentStyle(IsHovering, isPressed, Disabled, IsFocused);
    }

    internal void PerformAutoWidth(IDWriteFactory dwriteFactory)
    {
        if (!AutoWidth || dwriteFactory is null || Themes is null)
        {
            return;
        }

        Vector2 textSize = UI.Resources.MeasureText(dwriteFactory, Text, Themes.Normal);
        float desiredWidth = textSize.X + TextMargin.X * 2;

        if (desiredWidth > 0 && Math.Abs(Size.X - desiredWidth) > 0.1f)
        {
            Size = new Vector2(desiredWidth, Size.Y);
        }
    }

    private void DrawBackground(ID2D1RenderTarget renderTarget)
    {
        Rect bounds = GlobalBounds;
        ButtonStyle? style = Themes?.Current;
        if (style is null || bounds.Width <= 0 || bounds.Height <= 0) return;

        UI.Resources.DrawBoxStyleHelper(renderTarget, new Vector2(bounds.X, bounds.Y), new Vector2(bounds.Width, bounds.Height), style);
    }

    private void DrawText(ID2D1RenderTarget renderTarget, IDWriteFactory dwriteFactory)
    {
        if (string.IsNullOrEmpty(Text)) return;
        ButtonStyle? style = Themes?.Current;
        if (style is null) return;

        ID2D1SolidColorBrush textBrush = UI.Resources.GetOrCreateBrush(renderTarget, style.FontColor);
        if (textBrush is null) return;

        Rect bounds = GlobalBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        try
        {
            var layoutKey = new UIResources.TextLayoutCacheKey(Text, style, new Vector2(bounds.Width, bounds.Height), TextAlignment);
            if (!UI.Resources.textLayoutCache.TryGetValue(layoutKey, out var textLayout))
            {
                IDWriteTextFormat? textFormat = UI.Resources.GetOrCreateTextFormat(dwriteFactory, style);
                if (textFormat is null) return;

                textLayout = dwriteFactory.CreateTextLayout(Text, textFormat, bounds.Width, bounds.Height);
                textLayout.TextAlignment = TextAlignment.Horizontal switch
                {
                    HAlignment.Left => Vortice.DirectWrite.TextAlignment.Leading,
                    HAlignment.Center => Vortice.DirectWrite.TextAlignment.Center,
                    HAlignment.Right => Vortice.DirectWrite.TextAlignment.Trailing,
                    _ => Vortice.DirectWrite.TextAlignment.Leading
                };
                textLayout.ParagraphAlignment = TextAlignment.Vertical switch
                {
                    VAlignment.Top => ParagraphAlignment.Near,
                    VAlignment.Center => ParagraphAlignment.Center,
                    VAlignment.Bottom => ParagraphAlignment.Far,
                    _ => ParagraphAlignment.Near
                };
                UI.Resources.textLayoutCache[layoutKey] = textLayout;
            }

            var textOrigin = new Vector2(bounds.X + TextOffset.X, bounds.Y + TextOffset.Y);
            renderTarget.DrawTextLayout(textOrigin, textLayout, textBrush, DrawTextOptions.None);
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Button Text Draw failed (RecreateTarget): {ex.Message}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error drawing button text: {ex.Message}");
        }
    }
}