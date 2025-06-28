namespace DirectUI;

internal class ResizableHPanelState
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
}