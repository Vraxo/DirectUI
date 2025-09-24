namespace Agex;

public class FileNode
{
    public required string Name { get; set; }
    public required string Kind { get; set; }
    public List<FileNode> Children { get; set; } = [];
}