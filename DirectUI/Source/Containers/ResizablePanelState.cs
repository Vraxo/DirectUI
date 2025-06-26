namespace DirectUI;

internal class ResizablePanelState
{
    internal string Id { get; }
    internal VBoxContainerState InnerVBox { get; }
    internal bool ClipRectWasPushed { get; }

    internal ResizablePanelState(string id, VBoxContainerState innerVBox, bool clipPushed)
    {
        Id = id;
        InnerVBox = innerVBox;
        ClipRectWasPushed = clipPushed;
    }
}