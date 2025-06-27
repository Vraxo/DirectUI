namespace DirectUI;

internal class TreeViewState
{
    internal string Id { get; }
    internal TreeStyle Style { get; }
    internal Stack<bool> IndentLineState { get; } = new();

    internal TreeViewState(string id, TreeStyle style)
    {
        Id = id;
        Style = style;
    }
}