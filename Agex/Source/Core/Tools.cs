using System.Text.RegularExpressions;

namespace Agex.Core;

public static class Tools
{
    private static string ResolveAndValidatePath(string basePath, string userPath)
    {
        if (string.IsNullOrWhiteSpace(userPath))
        {
            throw new ArgumentException("Path must be a non-empty string.", nameof(userPath));
        }

        // Normalize slashes for consistency
        string normalizedUserPath = userPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

        string resolvedPath = Path.GetFullPath(Path.Combine(basePath, normalizedUserPath));
        string normalizedBasePath = Path.GetFullPath(basePath);

        if (!resolvedPath.StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Path traversal detected. Operation on '{userPath}' is not allowed outside the project directory.");
        }
        return resolvedPath;
    }

    public static async Task<string> CreateFile(string projectPath, string relativePath, string content = "")
    {
        var fullPath = ResolveAndValidatePath(projectPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content);
        return $"Created file: {relativePath}";
    }

    public static Task<string> DeleteFile(string projectPath, string relativePath)
    {
        var fullPath = ResolveAndValidatePath(projectPath, relativePath);
        File.Delete(fullPath);
        return Task.FromResult($"Deleted file: {relativePath}");
    }

    public static Task<string> MakeDir(string projectPath, string relativePath)
    {
        var fullPath = ResolveAndValidatePath(projectPath, relativePath);
        Directory.CreateDirectory(fullPath);
        return Task.FromResult($"Created directory: {relativePath}");
    }

    public static async Task<string> ReadFile(string projectPath, string relativePath)
    {
        var fullPath = ResolveAndValidatePath(projectPath, relativePath);
        var content = await File.ReadAllTextAsync(fullPath);
        return $"Content of {relativePath}:\n---\n{content}";
    }

    public static async Task<string> EditFile(string projectPath, string relativePath, string find, string replace)
    {
        var fullPath = ResolveAndValidatePath(projectPath, relativePath);
        var originalContent = await File.ReadAllTextAsync(fullPath);

        // This regex logic is a direct port from the TS version to maintain behavior.
        var escapedFind = Regex.Escape(find);
        var findRegexPattern = string.Join("\\s+", escapedFind.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        var findRegex = new Regex(findRegexPattern, RegexOptions.Multiline);
        var match = findRegex.Match(originalContent);

        if (!match.Success)
        {
            throw new InvalidOperationException("The 'find' string was not found in the file. Please ensure the 'find' block's content is correct.");
        }

        var lineStartIndex = originalContent.LastIndexOf('\n', Math.Max(0, match.Index - 1)) + 1;
        var leadingWhitespace = originalContent.Substring(lineStartIndex, match.Index - lineStartIndex);

        // Construct the replacement string with original indentation
        var indentedReplace = string.Join("\n", replace.Split('\n').Select(line => leadingWhitespace + line));

        var newContent = originalContent.Remove(match.Index, match.Length).Insert(match.Index, indentedReplace);

        await File.WriteAllTextAsync(fullPath, newContent);
        return $"Replaced content in {relativePath}";
    }

    public static Task<string> MoveFile(string projectPath, string srcRelative, string destRelative)
    {
        var srcPath = ResolveAndValidatePath(projectPath, srcRelative);
        var destPath = ResolveAndValidatePath(projectPath, destRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        Directory.Move(srcPath, destPath);
        return Task.FromResult($"Moved {srcRelative} to {destRelative}");
    }

    public static Task<string> RenameFile(string projectPath, string srcRelative, string newName)
    {
        var srcPath = ResolveAndValidatePath(projectPath, srcRelative);
        var destPath = Path.Combine(Path.GetDirectoryName(srcPath)!, newName);

        ResolveAndValidatePath(projectPath, Path.GetRelativePath(projectPath, destPath));

        Directory.Move(srcPath, destPath);
        var destRelative = Path.GetRelativePath(projectPath, destPath).Replace('\\', '/');
        return Task.FromResult($"Renamed {srcRelative} to {destRelative}");
    }

    public static async Task<string> Search(string projectPath, string startRelativePath, string pattern, bool searchContent)
    {
        var regex = new Regex(pattern);
        var results = new List<string>();
        var startPath = string.IsNullOrEmpty(startRelativePath) || startRelativePath == "."
            ? projectPath
            : ResolveAndValidatePath(projectPath, startRelativePath);

        var allFiles = Directory.EnumerateFiles(startPath, "*", SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            var relativePath = Path.GetRelativePath(projectPath, file).Replace('\\', '/');
            if (searchContent)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    if (regex.IsMatch(content)) results.Add(relativePath);
                }
                catch { /* Ignore unreadable files */ }
            }
            else
            {
                if (regex.IsMatch(Path.GetFileName(file))) results.Add(relativePath);
            }
        }

        return results.Count > 0 ? $"Found matches:\n{string.Join("\n", results)}" : $"No matches found for pattern \"{pattern}\" in {startRelativePath ?? "root"}.";
    }
}