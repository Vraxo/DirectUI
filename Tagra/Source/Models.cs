using System.Collections.Generic;

namespace Tagra.Data;

public class Tag
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public int FileCount { get; set; }

    public override string ToString() => Name;
}

public class FileEntry
{
    public long Id { get; set; }
    public required string Path { get; set; }
    public string? Hash { get; set; }
    public List<Tag> Tags { get; set; } = new();
}

public class TagComparer : IEqualityComparer<Tag>
{
    public bool Equals(Tag? x, Tag? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        return x.Id == y.Id;
    }

    public int GetHashCode(Tag obj)
    {
        return obj.Id.GetHashCode();
    }
}