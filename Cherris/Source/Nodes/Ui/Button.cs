using Vortice.Mathematics;

namespace Cherris;

public class Button : Control
{
    public enum ActionMode { Release, Press }
    public enum ClickBehavior { Left, Right, Both }

    private bool pressedLeft = false;
    private bool pressedRight = false;
    private bool wasHovered = false;
    private string displayedText = "";

    public Vector2 TextOffset { get; set; } = Vector2.Zero;
    public HAlignment TextHAlignment { get; set; } = HAlignment.Center;
    public VAlignment TextVAlignment { get; set; } = VAlignment.Center;
    public ButtonStylePack Styles { get; set; } = new();
    public ActionMode LeftClickActionMode { get; set; } = ActionMode.Release;
    public ActionMode RightClickActionMode { get; set; } = ActionMode.Release;
    public ClickBehavior Behavior { get; set; } = ClickBehavior.Left;
    public float AvailableWidth { get; set; } = 0;
    public bool StayPressed { get; set; } = false;
    public bool ClipText { get; set; } = false;
    public bool AutoWidth { get; set; } = false;
    public Vector2 TextMargin { get; set; } = new(2, 2);
    public string Ellipsis { get; set; } = "...";
    public Texture? Icon { get; set; } = null;
    public float IconMargin { get; set; } = 12;
    public Sound? ClickSound { get; set; }
    public Sound? HoverSound { get; set; }

    public string Text
    {
        get => displayedText;
        set
        {
            if (displayedText == value)            {
                return;
            }

            displayedText = value;        }
    }

    public event Action? LeftClicked;
    public event Action? RightClicked;
    public event Action? MouseEntered;
    public event Action? MouseExited;

    public Button()
    {
        Size = new(100, 26);
        Offset = Vector2.Zero;
        OriginPreset = OriginPreset.None;

        WasDisabled += (button) =>
        {
            Styles.Current = Disabled
            ? Styles.Disabled
            : Styles.Normal;
        };
    }

    public override void Process()
    {
        base.Process();

        if (Disabled)
        {
            return;
        }

        HandleClicks();
        HandleKeyboardInput();
    }

    protected virtual void OnEnterPressed() { }

    private void HandleKeyboardInput()
    {
        bool enterPressed = Input.IsKeyPressed(KeyCode.Enter);

        if (!Focused || !enterPressed)
        {
            return;
        }

        bool invoked = false;
        if (Behavior == ClickBehavior.Left || Behavior == ClickBehavior.Both)
        {
            LeftClicked?.Invoke();
            invoked = true;
        }

        if (Behavior == ClickBehavior.Right || Behavior == ClickBehavior.Both)
        {
            RightClicked?.Invoke();
            invoked = true;
        }

        if (invoked)
        {
            ClickSound?.Play(AudioBus);
            OnEnterPressed();
        }
    }

    private void HandleClicks()
    {
        bool isMouseOver = IsMouseOver();
        bool leftClickInvoked = false;
        bool rightClickInvoked = false;

        if (Behavior == ClickBehavior.Left || Behavior == ClickBehavior.Both)
        {
            leftClickInvoked = HandleSingleClick(
                ref pressedLeft,
                MouseButtonCode.Left,
                LeftClickActionMode,
                LeftClicked);
        }

        if (Behavior == ClickBehavior.Right || Behavior == ClickBehavior.Both)
        {
            rightClickInvoked = HandleSingleClick(
                ref pressedRight,
                MouseButtonCode.Right,
                RightClickActionMode,
                RightClicked);
        }

        HandleHover(isMouseOver);
        UpdateTheme(isMouseOver, pressedLeft || pressedRight);
    }

    private bool HandleSingleClick(ref bool pressedState, MouseButtonCode button, ActionMode mode, Action? handler)
    {
        bool invoked = false;
        bool mouseOver = IsMouseOver();
        bool buttonPressedThisFrame = Input.IsMouseButtonPressed(button);
        bool buttonReleasedThisFrame = Input.IsMouseButtonReleased(button);

        if (mouseOver && buttonPressedThisFrame && !Disabled)
        {
            pressedState = true;
            HandleClickFocus();

            if (mode == ActionMode.Press)
            {
                handler?.Invoke();
                ClickSound?.Play(AudioBus);
                invoked = true;
            }
        }

        if (!buttonReleasedThisFrame || !pressedState)
        {
            return invoked;
        }

        if (!Disabled && mouseOver && mode == ActionMode.Release)
        {
            handler?.Invoke();
            ClickSound?.Play(AudioBus);
            invoked = true;
        }

        if (!StayPressed)
        {
            pressedState = false;
        }
        else if (!mouseOver && mode == ActionMode.Release)
        {
            pressedState = false;
        }
        else if (mode == ActionMode.Press && !mouseOver)
        {
            pressedState = false;
        }

        return invoked;
    }

    private void HandleHover(bool isMouseOver)
    {
        if (Disabled)
        {
            if (wasHovered)
            {
                wasHovered = false;
                MouseExited?.Invoke();
            }

            return;
        }

        if (isMouseOver && !wasHovered)
        {
            MouseEntered?.Invoke();
            HoverSound?.Play(AudioBus);
            wasHovered = true;
        }
        else if (wasHovered && !isMouseOver)        {
            wasHovered = false;
            MouseExited?.Invoke();
        }
    }

    private void UpdateTheme(bool isMouseOver, bool isPressedForStayPressed)
    {
        if (Disabled)
        {
            Styles.Current = Styles.Disabled;
            return;
        }

        bool isLeftDown = (Behavior == ClickBehavior.Left || Behavior == ClickBehavior.Both) && Input.IsMouseButtonDown(MouseButtonCode.Left);
        bool isRightDown = (Behavior == ClickBehavior.Right || Behavior == ClickBehavior.Both) && Input.IsMouseButtonDown(MouseButtonCode.Right);

        bool isPhysicallyHeldDown = isMouseOver && (isLeftDown || isRightDown);

        if (isPressedForStayPressed && StayPressed)
        {
            Styles.Current = Styles.Pressed;
        }
        else if (isPhysicallyHeldDown)
        {
            Styles.Current = Styles.Pressed;
        }
        else if (Focused)
        {
            Styles.Current = isMouseOver ? Styles.Hover : Styles.Focused;
        }
        else if (isMouseOver)
        {
            Styles.Current = Styles.Hover;
        }
        else
        {
            Styles.Current = Styles.Normal;
        }
    }

    protected override void OnThemeFileChanged(string themeFile)
    {
        Log.Warning($"OnThemeFileChanged not fully implemented for Button: {themeFile}");
    }

    public override void Draw(DrawingContext context)
    {
        if (!Visible)
        {
            return;
        }

        DrawButtonBackground(context);
        DrawIcon(context);        DrawButtonText(context);
    }

    protected virtual void DrawButtonBackground(DrawingContext context)
    {
        Vector2 position = GlobalPosition - Origin;
        Vector2 size = ScaledSize;
        Rect bounds = new(position.X, position.Y, size.X, size.Y);

        DrawStyledRectangle(context, bounds, Styles.Current);
    }

    private void DrawIcon(DrawingContext context)    {
        if (Icon is null || context.RenderTarget is null)
        {
            return;
        }

        Log.Warning("DrawIcon is not implemented.");
    }

    protected virtual void DrawButtonText(DrawingContext context)
    {
        if (Styles.Current is null || string.IsNullOrEmpty(displayedText))        {
            return;
        }

        Vector2 position = GlobalPosition - Origin;
        Vector2 size = ScaledSize;
        float textStartX = position.X + TextMargin.X + TextOffset.X;
        float textAvailableWidth = Math.Max(0, size.X - TextMargin.X * 2);

        if (Icon != null)
        {
            Log.Warning("Button.DrawButtonText icon adjustment not fully implemented.");
        }


        var textLayoutRect = new Rect(
            textStartX,
            position.Y + TextMargin.Y + TextOffset.Y,
            textAvailableWidth,
            Math.Max(0, size.Y - TextMargin.Y * 2)
        );

        DrawFormattedText(
            context,
            displayedText,            textLayoutRect,
            Styles.Current,
            TextHAlignment,
            TextVAlignment
        );
    }

    private void ResizeToFitText()
    {
        if (!AutoWidth || Styles?.Current is null)
        {
            return;
        }

        Log.Warning("ResizeToFitText requires DirectWrite implementation.");
    }

    private void ClipDisplayedText()
    {
        if (!ClipText || string.IsNullOrEmpty(Text) || Styles?.Current is null)
        {
            return;
        }

        Log.Warning("ClipDisplayedText requires DirectWrite implementation.");
    }

    private string GetTextClippedWithEllipsis(string input)
    {
        if (input.Length > Ellipsis.Length)
        {
            return input.Substring(0, input.Length - Ellipsis.Length) + Ellipsis;
        }
        return input;
    }
}