// Core/UIContext.cs
using System.Collections.Generic;
using DirectUI.Core;
using DirectUI.Drawing;

namespace DirectUI;

public class UIContext
{
    // Per-frame services and state
    public IRenderer Renderer { get; internal set; }
    public ITextService TextService { get; }
    public InputState InputState { get; }
    public float DeltaTime { get; }
    public float TotalTime { get; }
    public float UIScale { get; }
    public UIPersistentState State { get; internal set; } = null!;

    // Layout and state management
    public UILayoutManager Layout { get; internal set; }
    internal readonly Stack<TreeViewState> treeStateStack = new();
    internal readonly Stack<(StyleVar, object)> styleVarStack = new();
    internal readonly Stack<(StyleColor, Color)> styleColorStack = new();

    public UIContext(IRenderer renderer, ITextService textService, InputState inputState, float deltaTime, float totalTime, float uiScale)
    {
        Renderer = renderer;
        TextService = textService;
        InputState = inputState;
        DeltaTime = deltaTime;
        TotalTime = totalTime;
        UIScale = uiScale;
        Layout = new UILayoutManager(uiScale);
    }
}