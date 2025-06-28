namespace DirectUI;

internal class TreeViewState
{
    internal int Id { get; }
    internal TreeStyle Style { get; }
    internal Stack<bool> IndentLineState { get; } = new();

    internal TreeViewState(int id, TreeStyle style)
    {
        Id = id;
        Style = style;
    }
}