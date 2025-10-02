using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tagra.Data;

public class DatabaseManager
{
    private readonly string _databasePath;
    private static readonly HashSet<string> _searchStopWords = new(StringComparer.OrdinalIgnoreCase) { "and", "or", "not" };

    // A palette of default colors for new tags
    private static readonly List<string> _defaultTagColors = new()
    {
        "#e53935", "#d81b60", "#8e24aa", "#5e35b1", "#3949ab",
        "#1e88e5", "#039be5", "#00acc1", "#00897b", "#43a047",
        "#7cb342", "#c0ca33", "#fdd835", "#ffb300", "#fb8c00",
        "#f4511e", "#6d4c41", "#757575", "#546e7a"
    };
    private static int _nextColorIndex = 0;

    public DatabaseManager()
    {
        _databasePath = Path.Combine(AppContext.BaseDirectory, "tagra.db");
        InitializeDatabase();
    }

    private SqliteConnection GetConnection() => new($"Data Source={_databasePath}");

    private void InitializeDatabase()
    {
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
            CREATE TABLE IF NOT EXISTS Tags (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                ColorHex TEXT NOT NULL DEFAULT '#757575'
            );

            CREATE TABLE IF NOT EXISTS Files (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Path TEXT NOT NULL UNIQUE,
                Hash TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS FileTags (
                FileId INTEGER NOT NULL,
                TagId INTEGER NOT NULL,
                FOREIGN KEY(FileId) REFERENCES Files(Id) ON DELETE CASCADE,
                FOREIGN KEY(TagId) REFERENCES Tags(Id) ON DELETE CASCADE,
                PRIMARY KEY (FileId, TagId)
            );
        ";
        command.ExecuteNonQuery();

        // --- Simple Migration: Add ColorHex column if it doesn't exist ---
        command.CommandText = "PRAGMA table_info(Tags);";
        bool colorHexColumnExists = false;
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                if (reader.GetString(1).Equals("ColorHex", StringComparison.OrdinalIgnoreCase))
                {
                    colorHexColumnExists = true;
                    break;
                }
            }
        }

        if (!colorHexColumnExists)
        {
            Console.WriteLine("[Database] Old schema detected. Upgrading 'Tags' table...");
            command.CommandText = "ALTER TABLE Tags ADD COLUMN ColorHex TEXT NOT NULL DEFAULT '#757575';";
            command.ExecuteNonQuery();

            // Assign colors to existing tags so they aren't all the default gray.
            var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = "SELECT Id FROM Tags ORDER BY Id";
            var tagIds = new List<long>();
            using (var reader = updateCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    tagIds.Add(reader.GetInt64(0));
                }
            }

            for (int i = 0; i < tagIds.Count; i++)
            {
                var color = _defaultTagColors[i % _defaultTagColors.Count];
                var colorUpdateCmd = connection.CreateCommand();
                colorUpdateCmd.CommandText = "UPDATE Tags SET ColorHex = @ColorHex WHERE Id = @Id";
                colorUpdateCmd.Parameters.AddWithValue("@ColorHex", color);
                colorUpdateCmd.Parameters.AddWithValue("@Id", tagIds[i]);
                colorUpdateCmd.ExecuteNonQuery();
            }
            Console.WriteLine("[Database] Upgrade complete.");
        }
        // --- End of Migration ---
    }

    public List<Tag> GetAllTags()
    {
        var tags = new List<Tag>();
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT T.Id, T.Name, COUNT(FT.FileId), T.ColorHex
            FROM Tags T
            LEFT JOIN FileTags FT ON T.Id = FT.TagId
            GROUP BY T.Id, T.Name
            ORDER BY T.Name";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tags.Add(new Tag
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                FileCount = reader.GetInt32(2),
                ColorHex = reader.GetString(3)
            });
        }
        return tags;
    }

    public bool AddTag(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        using var connection = GetConnection();
        connection.Open();

        // Get the current number of tags to pick a color
        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Tags";
        var tagCount = Convert.ToInt32(countCommand.ExecuteScalar());
        var color = _defaultTagColors[tagCount % _defaultTagColors.Count];

        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Tags (Name, ColorHex) VALUES (@Name, @ColorHex)";
        command.Parameters.AddWithValue("@Name", name.Trim().ToLower());
        command.Parameters.AddWithValue("@ColorHex", color);

        try
        {
            return command.ExecuteNonQuery() > 0;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // UNIQUE constraint violation
        {
            Console.WriteLine($"Tag '{name}' already exists.");
            return false;
        }
    }

    public void UpdateTagColor(long tagId, string newColorHex)
    {
        using var connection = GetConnection();
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Tags SET ColorHex = @ColorHex WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", tagId);
        command.Parameters.AddWithValue("@ColorHex", newColorHex);
        command.ExecuteNonQuery();
    }

    public FileEntry? AddFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        using var connection = GetConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT Id FROM Files WHERE Path = @Path";
            command.Parameters.AddWithValue("@Path", path);
            var existingId = command.ExecuteScalar();
            if (existingId != null)
            {
                return GetFileById(Convert.ToInt64(existingId));
            }
            command.CommandText = "INSERT INTO Files (Path) VALUES (@Path); SELECT last_insert_rowid();";
            long newId = Convert.ToInt64(command.ExecuteScalar());
            transaction.Commit();
            return new FileEntry { Id = newId, Path = path };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding file: {ex.Message}");
            transaction.Rollback();
            return null;
        }
    }

    public FileEntry? GetFileById(long fileId)
    {
        using var connection = GetConnection();
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Path, Hash FROM Files WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", fileId);
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new FileEntry
            {
                Id = reader.GetInt64(0),
                Path = reader.GetString(1),
                Hash = reader.IsDBNull(2) ? null : reader.GetString(2)
            };
        }
        return null;
    }

    public List<FileEntry> GetAllFiles()
    {
        var files = new List<FileEntry>();
        using var connection = GetConnection();
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Path, Hash FROM Files ORDER BY Path";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            files.Add(new FileEntry
            {
                Id = reader.GetInt64(0),
                Path = reader.GetString(1),
                Hash = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }
        return files;
    }

    public List<Tag> GetTagsForFile(long fileId)
    {
        var tags = new List<Tag>();
        using var connection = GetConnection();
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT T.Id, T.Name, (SELECT COUNT(*) FROM FileTags WHERE TagId = T.Id), T.ColorHex
            FROM Tags T
            INNER JOIN FileTags FT ON T.Id = FT.TagId
            WHERE FT.FileId = @FileId
            ORDER BY T.Name";
        command.Parameters.AddWithValue("@FileId", fileId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tags.Add(new Tag { Id = reader.GetInt64(0), Name = reader.GetString(1), FileCount = reader.GetInt32(2), ColorHex = reader.GetString(3) });
        }
        return tags;
    }

    public void AddTagToFile(long fileId, long tagId)
    {
        using var connection = GetConnection();
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO FileTags (FileId, TagId) VALUES (@FileId, @TagId)";
        command.Parameters.AddWithValue("@FileId", fileId);
        command.Parameters.AddWithValue("@TagId", tagId);
        command.ExecuteNonQuery();
    }

    public void RemoveTagFromFile(long fileId, long tagId)
    {
        using var connection = GetConnection();
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM FileTags WHERE FileId = @FileId AND TagId = @TagId";
        command.Parameters.AddWithValue("@FileId", fileId);
        command.Parameters.AddWithValue("@TagId", tagId);
        command.ExecuteNonQuery();
    }

    public List<FileEntry> GetFilesByTags(string searchText)
    {
        var searchTokens = searchText.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var searchTags = searchTokens.Where(token => !_searchStopWords.Contains(token)).Distinct().ToList();

        if (searchTags.Count == 0)
        {
            return GetAllFiles();
        }

        var files = new List<FileEntry>();
        using var connection = GetConnection();
        connection.Open();
        var command = connection.CreateCommand();

        var parameterNames = new List<string>();
        for (int i = 0; i < searchTags.Count; i++)
        {
            string paramName = $"@tag{i}";
            parameterNames.Add(paramName);
            command.Parameters.AddWithValue(paramName, searchTags[i]);
        }

        command.CommandText = $@"
            SELECT F.Id, F.Path, F.Hash
            FROM Files F
            INNER JOIN FileTags FT ON F.Id = FT.FileId
            INNER JOIN Tags T ON FT.TagId = T.Id
            WHERE T.Name IN ({string.Join(",", parameterNames)})
            GROUP BY F.Id
            HAVING COUNT(DISTINCT T.Id) = {searchTags.Count}
            ORDER BY F.Path";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            files.Add(new FileEntry
            {
                Id = reader.GetInt64(0),
                Path = reader.GetString(1),
                Hash = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }
        return files;
    }

    public void DeleteTag(long tagId)
    {
        using var connection = GetConnection();
        connection.Open();
        var command = connection.CreateCommand();
        // Thanks to "ON DELETE CASCADE", entries in FileTags will be removed automatically.
        command.CommandText = "DELETE FROM Tags WHERE Id = @TagId";
        command.Parameters.AddWithValue("@TagId", tagId);
        command.ExecuteNonQuery();
    }

    // Placeholder methods for future implementation
    public void UpdateTagsForFile(long fileId, IEnumerable<long> tagIds) { /* ... */ }
}