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
                Name TEXT NOT NULL UNIQUE
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

        // For testing: add some default tags if none exist
        command.CommandText = "SELECT COUNT(*) FROM Tags";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0)
        {
            AddTag("work");
            AddTag("personal");
            AddTag("photos");
            AddTag("documents");
        }
    }

    public List<Tag> GetAllTags()
    {
        var tags = new List<Tag>();
        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT T.Id, T.Name, COUNT(FT.FileId)
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
                FileCount = reader.GetInt32(2)
            });
        }
        return tags;
    }

    public bool AddTag(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        using var connection = GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Tags (Name) VALUES (@Name)";
        command.Parameters.AddWithValue("@Name", name.Trim().ToLower());

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
            SELECT T.Id, T.Name, (SELECT COUNT(*) FROM FileTags WHERE TagId = T.Id)
            FROM Tags T
            INNER JOIN FileTags FT ON T.Id = FT.TagId
            WHERE FT.FileId = @FileId
            ORDER BY T.Name";
        command.Parameters.AddWithValue("@FileId", fileId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tags.Add(new Tag { Id = reader.GetInt64(0), Name = reader.GetString(1), FileCount = reader.GetInt32(2) });
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