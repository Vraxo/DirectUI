using System;
using System.Numerics;

namespace DirectUI;

public static partial class UI
{
    // --- Layout Helpers ---
    internal static Vector2 ApplyLayout(Vector2 defaultPosition) { return IsInLayoutContainer() ? GetCurrentLayoutPositionInternal() : defaultPosition; }
    internal static void AdvanceLayout(Vector2 elementSize) { if (IsInLayoutContainer()) { AdvanceLayoutCursor(new Vector2(Math.Max(0, elementSize.X), Math.Max(0, elementSize.Y))); } }
    internal static bool IsInLayoutContainer() => containerStack.Count > 0;
    internal static Vector2 GetCurrentLayoutPositionInternal()
    {
        if (containerStack.Count == 0) return Vector2.Zero;
        object currentContainerState = containerStack.Peek();
        return currentContainerState switch
        {
            HBoxContainerState hbox => hbox.CurrentPosition,
            VBoxContainerState vbox => vbox.CurrentPosition,
            GridContainerState grid => grid.CurrentDrawPosition,
            ResizablePanelState panel => panel.InnerVBox.CurrentPosition,
            ResizableHPanelState hpanel => hpanel.InnerHBox.CurrentPosition,
            _ => Vector2.Zero,
        };
    }
    public static Vector2 GetCurrentLayoutPosition() { return IsInLayoutContainer() ? GetCurrentLayoutPositionInternal() : Vector2.Zero; }
    internal static void AdvanceLayoutCursor(Vector2 elementSize)
    {
        if (containerStack.Count == 0) return;
        object currentContainerState = containerStack.Peek();
        switch (currentContainerState)
        {
            case HBoxContainerState hbox:
                {
                    if (elementSize.Y > hbox.MaxElementHeight) hbox.MaxElementHeight = elementSize.Y;

                    // Add the width of the element just drawn to the total
                    hbox.AccumulatedWidth += elementSize.X;
                    // Add the gap to the total width if this wasn't the first element
                    if (hbox.ElementCount > 0)
                    {
                        hbox.AccumulatedWidth += hbox.Gap;
                    }

                    // Advance the cursor for the next element's position
                    float advanceX = elementSize.X + hbox.Gap;
                    hbox.CurrentPosition = new Vector2(hbox.CurrentPosition.X + advanceX, hbox.CurrentPosition.Y);

                    hbox.ElementCount++;
                    break;
                }
            case VBoxContainerState vbox:
                {
                    if (elementSize.X > vbox.MaxElementWidth) vbox.MaxElementWidth = elementSize.X;

                    // Add the height of the element just drawn to the total
                    vbox.AccumulatedHeight += elementSize.Y;
                    // Add the gap to the total height if this wasn't the first element
                    if (vbox.ElementCount > 0)
                    {
                        vbox.AccumulatedHeight += vbox.Gap;
                    }

                    // Advance the cursor for the next element's position
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
                    // This is just a wrapper, so we pass the call down to its inner VBox.
                    // We need to get a mutable reference to the inner VBox to do this.
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
                    // This is just a wrapper, so we pass the call down to its inner HBox.
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