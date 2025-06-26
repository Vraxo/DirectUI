using System;
using System.Numerics;

namespace DirectUI;

public static partial class UI
{
    internal static Vector2 ApplyLayout(Vector2 defaultPosition) 
    { 
        return IsInLayoutContainer() 
            ? GetCurrentLayoutPositionInternal() 
            : defaultPosition; 
    }

    internal static void AdvanceLayout(Vector2 elementSize)
    {
        if (!IsInLayoutContainer())
        {
            return;
        }

        AdvanceLayoutCursor(new(Math.Max(0, elementSize.X), Math.Max(0, elementSize.Y)));
    }

    internal static bool IsInLayoutContainer() => containerStack.Count > 0;

    internal static Vector2 GetCurrentLayoutPositionInternal()
    {
        if (containerStack.Count == 0)
        {
            return Vector2.Zero;
        }

        object currentContainerState = containerStack.Peek();

        return currentContainerState switch
        {
            HBoxContainerState hbox => hbox.CurrentPosition,
            VBoxContainerState vbox => vbox.CurrentPosition,
            GridContainerState grid => grid.CurrentDrawPosition,
            ResizablePanelState panel => panel.InnerVBox.CurrentPosition,
            _ => Vector2.Zero,
        };
    }
    public static Vector2 GetCurrentLayoutPosition() 
    { 
        return IsInLayoutContainer() 
            ? GetCurrentLayoutPositionInternal() 
            : Vector2.Zero; 
    }
    
    internal static void AdvanceLayoutCursor(Vector2 elementSize)
    {
        if (containerStack.Count == 0)
        {
            return;
        }

        object currentContainerState = containerStack.Peek();

        switch (currentContainerState)
        {
            case HBoxContainerState hbox:
            {
                if (elementSize.Y > hbox.MaxElementHeight) hbox.MaxElementHeight = elementSize.Y;
                float advanceX = (hbox.AccumulatedWidth > 0 ? hbox.Gap : 0) + elementSize.X;
                hbox.CurrentPosition = new Vector2(hbox.CurrentPosition.X + advanceX, hbox.CurrentPosition.Y);
                hbox.AccumulatedWidth = hbox.CurrentPosition.X - hbox.StartPosition.X;
                break;
            }

            case VBoxContainerState vbox:
            {
                if (elementSize.X > vbox.MaxElementWidth)
                {
                    vbox.MaxElementWidth = elementSize.X;
                }

                float advanceY = (vbox.AccumulatedHeight > 0 ? vbox.Gap : 0) + elementSize.Y;
                vbox.CurrentPosition = new Vector2(vbox.CurrentPosition.X, vbox.CurrentPosition.Y + advanceY);
                vbox.AccumulatedHeight = vbox.CurrentPosition.Y - vbox.StartPosition.Y;
                break;
            }

            case GridContainerState grid:
            {
                grid.MoveToNextCell(elementSize);
                break;
            }
            case ResizablePanelState panel:
            {
                VBoxContainerState innerVBox = panel.InnerVBox;

                if (elementSize.X > innerVBox.MaxElementWidth)
                {
                    innerVBox.MaxElementWidth = elementSize.X;
                }

                float advanceY = (innerVBox.AccumulatedHeight > 0 ? innerVBox.Gap : 0) + elementSize.Y;
                innerVBox.CurrentPosition = new(innerVBox.CurrentPosition.X, innerVBox.CurrentPosition.Y + advanceY);
                innerVBox.AccumulatedHeight = innerVBox.CurrentPosition.Y - innerVBox.StartPosition.Y;
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