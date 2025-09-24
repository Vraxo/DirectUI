namespace Agex.Core;

public class FileNode
{
    public string Name { get; set; }
    public string Kind { get; set; } // "file" or "directory"
    public List<FileNode> Children { get; set; } = new();

    public FileNode(string name, string kind)
    {
        Name = name;
        Kind = kind;
    }
}