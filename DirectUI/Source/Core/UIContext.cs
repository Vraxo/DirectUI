using System.Collections.Generic;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace DirectUI;

public class UIContext
{
    // Per-frame resources
    public ID2D1HwndRenderTarget RenderTarget { get; }
    public IDWriteFactory DWriteFactory { get; }
    public InputState InputState { get; }
    public UIResources Resources { get; }

    // Layout and state management
    public UILayoutManager Layout { get; }
    internal readonly Stack<TreeViewState> treeStateStack = new();
    internal readonly Stack<(StyleVar, object)> styleVarStack = new();
    internal readonly Stack<(StyleColor, Vortice.Mathematics.Color4)> styleColorStack = new();

    public UIContext(ID2D1HwndRenderTarget renderTarget, IDWriteFactory dwriteFactory, InputState inputState, UIResources resources)
    {
        RenderTarget = renderTarget;
        DWriteFactory = dwriteFactory;
        InputState = inputState;
        Resources = resources;
        Layout = new UILayoutManager();
    }
}