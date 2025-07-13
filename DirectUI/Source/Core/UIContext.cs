// Core/UIContext.cs
using System.Collections.Generic;
using DirectUI.Core;
using DirectUI.Drawing;

namespace DirectUI;

public class UIContext
{
    // Per-frame services and state
    public IRenderer Renderer { get; }
    public ITextService TextService { get; }
    public InputState InputState { get; }
    public float DeltaTime { get; }

    // Layout and state management
    public UILayoutManager Layout { get; }
    internal readonly Stack<TreeViewState> treeStateStack = new();
    internal readonly Stack<(StyleVar, object)> styleVarStack = new();
    internal readonly Stack<(StyleColor, Color)> styleColorStack = new();

    public UIContext(IRenderer renderer, ITextService textService, InputState inputState, float deltaTime)
    {
        Renderer = renderer;
        TextService = textService;
        InputState = inputState;
        DeltaTime = deltaTime;
        Layout = new UILayoutManager();
    }
}