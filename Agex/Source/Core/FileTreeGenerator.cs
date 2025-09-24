using System.Text;

namespace Agex;

public static class FileTreeGenerator
{
    private static readonly HashSet<string> ExcludedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "dist", "build", "out", "release", "target", ".git",
        ".svn", ".hg", ".vscode", ".idea", "coverage", "__pycache__",
        ".pytest_cache", "vendor", "bower_components", ".next", ".nuxt", "tmp", "temp",
        "bin", "obj" // Added .NET build folders
    };

    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cfg", ".md5", ".ctex", ".ds_store", ".localized", "thumbs.db", "desktop.ini",
        ".o", ".obj", ".pyc", ".class", ".dll", ".exe", ".so", ".a", ".lib"
    };

    public static async Task<FileNode> GetDirectoryStructureAsync(string dirPath)
    {
        DirectoryInfo rootInfo = new(dirPath);
        FileNode rootNode = new() { Name = rootInfo.Name, Kind = "directory" };

        try
        {
            var entries = rootInfo.GetFileSystemInfos();
            var tasks = new List<Task<FileNode?>>();

            foreach (var entry in entries)
            {
                if (entry is DirectoryInfo dir)
                {
                    if (!dir.Name.StartsWith(".") && !ExcludedDirs.Contains(dir.Name))
                    {
                        tasks.Add(GetDirectoryStructureAsync(dir.FullName).ContinueWith(t => (FileNode?)t.Result));
                    }
                }
                else if (entry is FileInfo file)
                {
                    if (!ExcludedExtensions.Contains(file.Extension))
                    {
                        tasks.Add(Task.FromResult<FileNode?>(new() { Name = file.Name, Kind = "file"}));
                    }
                }
            }

            var children = await Task.WhenAll(tasks);
            rootNode.Children.AddRange(children.Where(c => c != null).Select(c => c!));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not read directory: {dirPath}, {ex.Message}");
        }

        return rootNode;
    }

    public static string CreateTreeText(FileNode structure)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{structure.Name}/");
        GenerateTreeText(structure, sb, "", true);
        return sb.ToString();
    }

    private static void GenerateTreeText(FileNode node, StringBuilder sb, string prefix, bool isRoot)
    {
        var children = node.Children
            .OrderBy(c => c.Kind == "directory" ? 0 : 1)
            .ThenBy(c => c.Name)
            .ToList();

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var isLast = i == children.Count - 1;
            var connector = isLast ? "└── " : "├── ";

            sb.Append(prefix).Append(connector).Append(child.Name);
            if (child.Kind == "directory") sb.Append("/");
            sb.AppendLine();

            if (child.Kind == "directory" && child.Children.Any())
            {
                var newPrefix = prefix + (isLast ? "    " : "│   ");
                GenerateTreeText(child, sb, newPrefix, false);
            }
        }
    }
}