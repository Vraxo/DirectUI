using System;
using System.IO;

namespace DirectUI;

public class SceneTreeView
{
    private readonly TreeNode<string> _fileRoot;
    private readonly TreeStyle _treeStyle = new();

    public TreeNode<string>? SelectedNode { get; private set; }

    public SceneTreeView()
    {
        try
        {
            // A hardcoded path for demonstration purposes.
            string scenePath = @"D:\Parsa Stuff\Visual Studio\Cosmocrush\Cosmocrush\Res\Scenes\Player.yaml";
            _fileRoot = File.Exists(scenePath)
                ? SceneParser.Parse(scenePath)
                : CreateDefaultTree("Scene file not found", scenePath);
        }
        catch (Exception ex)
        {
            _fileRoot = CreateDefaultTree($"Error parsing scene: {ex.Message}", "");
        }
        SelectedNode = null;
    }

    public void Draw()
    {
        UI.BeginVBoxContainer("tree_vbox", UI.Context.Layout.GetCurrentPosition(), 0);
        UI.Tree("file_tree", _fileRoot, out var clickedNode, _treeStyle);
        if (clickedNode is not null)
        {
            SelectedNode = clickedNode;
        }
        UI.EndVBoxContainer();
    }

    private static TreeNode<string> CreateDefaultTree(string reason, string path)
    {
        var root = new TreeNode<string>("Error", "Could not load scene", true);
        root.AddChild(reason, "");

        if (!string.IsNullOrEmpty(path))
        {
            root.AddChild($"Path: {path}", "");
        }

        return root;
    }
}