using System.Numerics;

namespace DirectUI;

public class UILayoutManager
{
    private readonly Stack<object> _containerStack = new();

    public int ContainerStackCount => _containerStack.Count;
    public bool IsInLayoutContainer() => _containerStack.Count > 0;

    public void PushContainer(object containerState) => _containerStack.Push(containerState);
    public object PopContainer() => _containerStack.Pop();
    public object PeekContainer() => _containerStack.Peek();
    public void ClearStack() => _containerStack.Clear();

    public Vector2 ApplyLayout(Vector2 defaultPosition)
    {
        return IsInLayoutContainer() ? GetCurrentPosition() : defaultPosition;
    }

    public void AdvanceLayout(Vector2 elementSize)
    {
        if (IsInLayoutContainer())
        {
            AdvanceContainerLayout(new Vector2(Math.Max(0, elementSize.X), Math.Max(0, elementSize.Y)));
        }
    }

    public Vector2 GetCurrentPosition()
    {
        if (_containerStack.Count == 0)
        {
            return Vector2.Zero;
        }

        return _containerStack.Peek() switch
        {
            HBoxContainerState hbox => hbox.CurrentPosition,
            VBoxContainerState vbox => vbox.CurrentPosition,
            GridContainerState grid => grid.CurrentDrawPosition,
            ResizablePanelState panel => panel.InnerVBox.CurrentPosition,
            ResizableHPanelState hpanel => hpanel.InnerHBox.CurrentPosition,
            _ => Vector2.Zero,
        };
    }

    public void AdvanceContainerLayout(Vector2 elementSize)
    {
        if (_containerStack.Count == 0) return;
        object currentContainerState = _containerStack.Peek();
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
                    Console.WriteLine("Error: AdvanceContainerLayout called with unexpected container type.");
                    break;
                }
        }
    }
}