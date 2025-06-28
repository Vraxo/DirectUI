namespace DirectUI;

internal class ResizablePanelState
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
}