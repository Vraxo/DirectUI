using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace DirectUI;

public class UIContext
{
    // Per-frame resources
    public ID2D1HwndRenderTarget RenderTarget { get; }
    public IDWriteFactory DWriteFactory { get; }
    public InputState InputState { get; }

    // Per-frame layout state
    internal readonly Stack<object> containerStack = new();
    internal readonly Stack<TreeViewState> treeStateStack = new();

    public UIContext(ID2D1HwndRenderTarget renderTarget, IDWriteFactory dwriteFactory, InputState inputState)
    {
        RenderTarget = renderTarget;
        DWriteFactory = dwriteFactory;
        InputState = inputState;
    }

    // --- Layout Helpers ---
    internal Vector2 ApplyLayout(Vector2 defaultPosition)
    {
        return IsInLayoutContainer() ? GetCurrentLayoutPositionInternal() : defaultPosition;
    }

    internal void AdvanceLayout(Vector2 elementSize)
    {
        if (IsInLayoutContainer())
        {
            AdvanceLayoutCursor(new Vector2(Math.Max(0, elementSize.X), Math.Max(0, elementSize.Y)));
        }
    }

    public bool IsInLayoutContainer() => containerStack.Count > 0;

    public Vector2 GetCurrentLayoutPosition()
    {
        return IsInLayoutContainer() ? GetCurrentLayoutPositionInternal() : Vector2.Zero;
    }

    private Vector2 GetCurrentLayoutPositionInternal()
    {
        if (containerStack.Count == 0)
        {
            return Vector2.Zero;
        }

        return containerStack.Peek() switch
        {
            HBoxContainerState hbox => hbox.CurrentPosition,
            VBoxContainerState vbox => vbox.CurrentPosition,
            GridContainerState grid => grid.CurrentDrawPosition,
            ResizablePanelState panel => panel.InnerVBox.CurrentPosition,
            ResizableHPanelState hpanel => hpanel.InnerHBox.CurrentPosition,
            _ => Vector2.Zero,
        };
    }

    internal void AdvanceLayoutCursor(Vector2 elementSize)
    {
        if (containerStack.Count == 0) return;
        object currentContainerState = containerStack.Peek();
        switch (currentContainerState)
        {
            case HBoxContainerState hbox:
                {
                    if (elementSize.Y > hbox.MaxElementHeight) hbox.MaxElementHeight = elementSize.Y;
                    hbox.AccumulatedWidth += elementSize.X;
                    if (hbox.ElementCount > 0)
                    {
                        hbox.AccumulatedWidth += hbox.Gap;
                    }
                    float advanceX = elementSize.X + hbox.Gap;
                    hbox.CurrentPosition = new Vector2(hbox.CurrentPosition.X + advanceX, hbox.CurrentPosition.Y);
                    hbox.ElementCount++;
                    break;
                }
            case VBoxContainerState vbox:
                {
                    if (elementSize.X > vbox.MaxElementWidth) vbox.MaxElementWidth = elementSize.X;
                    vbox.AccumulatedHeight += elementSize.Y;
                    if (vbox.ElementCount > 0)
                    {
                        vbox.AccumulatedHeight += vbox.Gap;
                    }
                    float advanceY = elementSize.Y + vbox.Gap;
                    vbox.CurrentPosition = new Vector2(vbox.CurrentPosition.X, vbox.CurrentPosition.Y + advanceY);
                    vbox.ElementCount++;
                    break;
                }
            case GridContainerState grid:
                {
                    grid.MoveToNextCell(elementSize);
                    break;
                }
            case ResizablePanelState panel:
                {
                    var innerVBox = panel.InnerVBox;
                    if (elementSize.X > innerVBox.MaxElementWidth) innerVBox.MaxElementWidth = elementSize.X;
                    innerVBox.AccumulatedHeight += elementSize.Y;
                    if (innerVBox.ElementCount > 0)
                    {
                        innerVBox.AccumulatedHeight += innerVBox.Gap;
                    }
                    float advanceY = elementSize.Y + innerVBox.Gap;
                    innerVBox.CurrentPosition = new Vector2(innerVBox.CurrentPosition.X, innerVBox.CurrentPosition.Y + advanceY);
                    innerVBox.ElementCount++;
                    break;
                }
            case ResizableHPanelState hpanel:
                {
                    var innerHBox = hpanel.InnerHBox;
                    if (elementSize.Y > innerHBox.MaxElementHeight) innerHBox.MaxElementHeight = elementSize.Y;
                    innerHBox.AccumulatedWidth += elementSize.X;
                    if (innerHBox.ElementCount > 0)
                    {
                        innerHBox.AccumulatedWidth += innerHBox.Gap;
                    }
                    float advanceX = elementSize.X + innerHBox.Gap;
                    innerHBox.CurrentPosition = new Vector2(innerHBox.CurrentPosition.X + advanceX, innerHBox.CurrentPosition.Y);
                    innerHBox.ElementCount++;
                    break;
                }
            default:
                {
                    Console.WriteLine("Error: AdvanceLayoutCursor called with unexpected container type.");
                    break;
                }
        }
    }
}