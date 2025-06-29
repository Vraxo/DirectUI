using System.Numerics;

namespace DirectUI;

internal class ResizableHPanelState : ILayoutContainer
{
    internal int Id { get; }
    internal HBoxContainerState InnerHBox { get; }
    internal bool ClipRectWasPushed { get; }

    internal ResizableHPanelState(int id, HBoxContainerState innerHBox, bool clipPushed)
    {
        Id = id;
        InnerHBox = innerHBox;
        ClipRectWasPushed = clipPushed;
    }

    public Vector2 GetCurrentPosition() => InnerHBox.GetCurrentPosition();

    public void Advance(Vector2 elementSize) => InnerHBox.Advance(elementSize);
}