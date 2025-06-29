using System.Numerics;

namespace DirectUI;

internal class ResizablePanelState : ILayoutContainer
{
    internal int Id { get; }
    internal VBoxContainerState InnerVBox { get; }
    internal bool ClipRectWasPushed { get; }

    internal ResizablePanelState(int id, VBoxContainerState innerVBox, bool clipPushed)
    {
        Id = id;
        InnerVBox = innerVBox;
        ClipRectWasPushed = clipPushed;
    }

    public Vector2 GetCurrentPosition() => InnerVBox.GetCurrentPosition();

    public void Advance(Vector2 elementSize) => InnerVBox.Advance(elementSize);
}