namespace DirectUI;

internal class ResizableHPanelState
{
    internal string Id { get; }
    internal HBoxContainerState InnerHBox { get; }
    internal bool ClipRectWasPushed { get; }

    internal ResizableHPanelState(string id, HBoxContainerState innerHBox, bool clipPushed)
    {
        Id = id;
        InnerHBox = innerHBox;
        ClipRectWasPushed = clipPushed;
    }
}