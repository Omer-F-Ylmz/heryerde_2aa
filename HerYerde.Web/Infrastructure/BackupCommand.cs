using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using Microsoft.Data.SqlClient;

namespace HerYerde.Web.Infrastructure;

public sealed record BackupSummary(string Database, string? Archive, IReadOnlyList<string> Removed);

public sealed record RestoreSummary(string Database, int Products);

/// <summary>Tek seferlik yedek komutları (docs/yedekleme.md). "--yedek-al klasör": SQL Server BACKUP DATABASE ile tarihli .bak
/// ve uploads klasörünün tar.gz arşivi; her türden en yeni 14 dosya kalır. "--yedek-yukle dosya": .bak'ı canlı veritabanına
/// değil "{ad}_prova" veritabanına açar. .bak yolu SQL Server'ın kendi diskindedir: klasör iki tarafa da aynı yoldan bağlanır.</summary>
public static class BackupCommand
{
    public const string BackupArgument = "--yedek-al";
    public const string RestoreArgument = "--yedek-yukle";

    /// <summary>Gecelik yedekte 14 gün.</summary>
    public const int Keep = 14;

    public const string RestoreSuffix = "_prova";

    public static string? BackupFolderFrom(string[] args) => ValueAfter(args, BackupArgument);

    public static string? RestoreFileFrom(string[] args) => ValueAfter(args, RestoreArgument);

    public static async Task<BackupSummary> BackupAsync(
        string connectionString,
        string uploadsPath,
        string folder,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(folder);
        var stamp = utcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var database = Path.Combine(folder, $"heryerde-{stamp}.bak");

        await using (var connection = await OpenMasterAsync(connectionString, cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandTimeout = 0;
            command.CommandText = "BACKUP DATABASE @database TO DISK = @file WITH INIT, COPY_ONLY, CHECKSUM";
            command.Parameters.AddWithValue("@database", new SqlConnectionStringBuilder(connectionString).InitialCatalog);
            command.Parameters.AddWithValue("@file", database);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        string? archive = null;
        if (Directory.Exists(uploadsPath))
        {
            archive = Path.Combine(folder, $"uploads-{stamp}.tar.gz");
            await using var file = File.Create(archive);
            await using var gzip = new GZipStream(file, CompressionLevel.Optimal);
            await TarFile.CreateFromDirectoryAsync(uploadsPath, gzip, includeBaseDirectory: false, cancellationToken);
        }

        var removed = Rotate(folder, "heryerde-*.bak").Concat(Rotate(folder, "uploads-*.tar.gz")).ToList();
        return new BackupSummary(database, archive, removed);
    }

    /// <summary>Prova veritabanı varsa üzerine yazılır; canlı veritabanına dokunulmaz.</summary>
    public static async Task<RestoreSummary> RestoreAsync(
        string connectionString,
        string backupFile,
        CancellationToken cancellationToken = default)
    {
        var target = new SqlConnectionStringBuilder(connectionString).InitialCatalog + RestoreSuffix;
        await using var connection = await OpenMasterAsync(connectionString, cancellationToken);

        var dataPath = (string)(await ScalarAsync(connection, "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000))", cancellationToken))!;
        var logPath = (string)(await ScalarAsync(connection, "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000))", cancellationToken))!;

        var moves = new List<(string Logical, string Physical)>();
        await using (var list = connection.CreateCommand())
        {
            list.CommandText = "RESTORE FILELISTONLY FROM DISK = @file";
            list.Parameters.AddWithValue("@file", backupFile);
            await using var reader = await list.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var logical = reader.GetString(reader.GetOrdinal("LogicalName"));
                var isLog = reader.GetString(reader.GetOrdinal("Type")) == "L";
                moves.Add((logical, isLog ? $"{logPath}{target}_{moves.Count}.ldf" : $"{dataPath}{target}_{moves.Count}.mdf"));
            }
        }

        await using (var restore = connection.CreateCommand())
        {
            restore.CommandTimeout = 0;
            var moveClauses = moves.Select((_, i) => $"MOVE @logical{i} TO @physical{i}");
            // Önceki prova açık bağlantı tutuyorsa RESTORE beklemesin.
            restore.CommandText =
                "DECLARE @quoted nvarchar(300) = QUOTENAME(@target), @sql nvarchar(max); " +
                "IF DB_ID(@target) IS NOT NULL BEGIN SET @sql = N'ALTER DATABASE ' + @quoted + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE'; EXEC(@sql); END; " +
                $"RESTORE DATABASE @target FROM DISK = @file WITH REPLACE, {string.Join(", ", moveClauses)}; " +
                "SET @sql = N'ALTER DATABASE ' + @quoted + N' SET MULTI_USER'; EXEC(@sql);";
            restore.Parameters.AddWithValue("@target", target);
            restore.Parameters.AddWithValue("@file", backupFile);
            for (var i = 0; i < moves.Count; i++)
            {
                restore.Parameters.AddWithValue($"@logical{i}", moves[i].Logical);
                restore.Parameters.AddWithValue($"@physical{i}", moves[i].Physical);
            }

            await restore.ExecuteNonQueryAsync(cancellationToken);
        }

        var products = (int)(await ScalarAsync(connection, $"SELECT COUNT(*) FROM {Quote(target)}.dbo.product", cancellationToken))!;
        return new RestoreSummary(target, products);
    }

    /// <summary>Adı zaman damgalı dosyalar ada göre sıralanınca tarih sırasına girer; en yeni <paramref name="keep"/> kalır.</summary>
    public static IReadOnlyList<string> Rotate(string folder, string pattern, int keep = Keep)
    {
        var stale = Directory.GetFiles(folder, pattern)
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .Skip(keep)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();
        stale.ForEach(File.Delete);
        return stale;
    }

    private static async Task<SqlConnection> OpenMasterAsync(string connectionString, CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<object?> ScalarAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static string Quote(string name) => "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";

    private static string? ValueAfter(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
