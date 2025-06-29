using System.Collections.Generic;

namespace DirectUI;

/// <summary>
/// Represents a node in a generic tree structure.
/// </summary>
/// <typeparam name="T">The type of the user data associated with the node.</typeparam>
public class TreeNode<T>
{
    public string Text { get; set; }
    public T UserData { get; set; }
    public bool IsExpanded { get; set; }
    public List<TreeNode<T>> Children { get; } = new();

    public TreeNode(string text, T userData, bool expanded = false)
    {
        Text = text;
        UserData = userData;
        IsExpanded = expanded;
    }

    /// <summary>
    /// Adds a new child node to this node.
    /// </summary>
    /// <param name="text">The display text of the child node.</param>
    /// <param name="userData">The user data associated with the child node.</param>
    /// <param name="expanded">Whether the child node is expanded by default.</param>
    /// <returns>The newly created child node.</returns>
    public TreeNode<T> AddChild(string text, T userData, bool expanded = false)
    {
        var child = new TreeNode<T>(text, userData, expanded);
        Children.Add(child);
        return child;
    }

    /// <summary>
    /// Adds an existing TreeNode as a child of this node.
    /// </summary>
    /// <param name="child">The child node to add.</param>
    /// <returns>The added child node.</returns>
    public TreeNode<T> AddChild(TreeNode<T> child)
    {
        Children.Add(child);
        return child;
    }
}