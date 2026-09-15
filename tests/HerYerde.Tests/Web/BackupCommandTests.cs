using System.Formats.Tar;
using System.IO.Compression;
using HerYerde.Web.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Web;

/// <summary>YAYIN-HAZIRLIK: --yedek-al tarihli .bak ve uploads arşivi yazar, 14'ten eskisini siler; --yedek-yukle ayrı
/// veritabanına açar. SQL Server dosyayı kendi diskine yazar: CI'da klasör konteynerle aynı yoldan paylaşılır
/// (HERYERDE_TEST_BACKUP_DIR).</summary>
[Collection(DatabaseCollection.Name)]
public sealed class BackupCommandTests : IAsyncLifetime
{
    private static readonly DateTime Moment = new(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);

    private readonly string _folder = SharedFolder();
    private readonly string _uploads = Path.Combine(Path.GetTempPath(), "heryerde-uploads-" + Guid.NewGuid().ToString("n"));

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        foreach (var directory in new[] { _folder, _uploads }.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Yedek_tarihli_bak_dosyasi_uretir()
    {
        var summary = await BackupCommand.BackupAsync(TestDb.ConnectionString, _uploads, _folder, Moment);

        Assert.Equal(Path.Combine(_folder, "heryerde-20260915-000000.bak"), summary.Database);
        Assert.True(new FileInfo(summary.Database).Length > 0);
    }

    [Fact]
    public void Rotasyon_on_besinci_dosyayi_siler()
    {
        Directory.CreateDirectory(_folder);
        for (var day = 1; day <= 15; day++)
        {
            File.WriteAllText(Path.Combine(_folder, $"heryerde-202609{day:00}-000000.bak"), "x");
        }

        File.WriteAllText(Path.Combine(_folder, "not.txt"), "x");

        var removed = BackupCommand.Rotate(_folder, "heryerde-*.bak");

        Assert.Equal([Path.Combine(_folder, "heryerde-20260901-000000.bak")], removed);
        Assert.Equal(14, Directory.GetFiles(_folder, "heryerde-*.bak").Length);
        Assert.True(File.Exists(Path.Combine(_folder, "not.txt")));
    }

    [Fact]
    public async Task Geri_yukleme_ayri_veritabanina_acilir_urun_sayisi_esit()
    {
        await using (var context = TestDb.NewContext())
        {
            await TestData.AddHomeProductAsync(context, "Cam Sürahi", "cam-surahi");
            await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        }

        var backup = await BackupCommand.BackupAsync(TestDb.ConnectionString, _uploads, _folder, Moment);

        var restored = await BackupCommand.RestoreAsync(TestDb.ConnectionString, backup.Database);

        var source = new SqlConnectionStringBuilder(TestDb.ConnectionString).InitialCatalog;
        Assert.Equal(source + "_prova", restored.Database);
        Assert.Equal(2, restored.Products);
        await using var original = TestDb.NewContext();
        Assert.Equal(2, await original.Products.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Uploads_klasoru_tar_gz_arsivine_girer()
    {
        Directory.CreateDirectory(Path.Combine(_uploads, "products", "7"));
        await File.WriteAllTextAsync(Path.Combine(_uploads, "products", "7", "a-800.webp"), "webp");

        var summary = await BackupCommand.BackupAsync(TestDb.ConnectionString, _uploads, _folder, Moment);

        Assert.Equal(Path.Combine(_folder, "uploads-20260915-000000.tar.gz"), summary.Archive);
        var entries = new List<string>();
        await using var gzip = new GZipStream(File.OpenRead(summary.Archive!), CompressionMode.Decompress);
        await using var reader = new TarReader(gzip);
        while (await reader.GetNextEntryAsync() is { } entry)
        {
            entries.Add(entry.Name.Replace('\\', '/'));
        }

        Assert.Contains(entries, name => name.EndsWith("products/7/a-800.webp", StringComparison.Ordinal));
    }

    /// <summary>D11: fatura ve dekontlar wwwroot dışındaki gizli klasörde; yedek onları ayrı arşive alır.</summary>
    [Fact]
    public async Task Gizli_belgeler_ayri_arsive_girer()
    {
        var documents = Path.Combine(_uploads, "gizli");
        Directory.CreateDirectory(Path.Combine(documents, "faturalar"));
        await File.WriteAllTextAsync(Path.Combine(documents, "faturalar", "f.pdf"), "%PDF-");

        var summary = await BackupCommand.BackupAsync(TestDb.ConnectionString, Path.Combine(_uploads, "yok"), _folder, Moment, documents);

        Assert.Null(summary.Archive);
        Assert.Equal(Path.Combine(_folder, "belgeler-20260915-000000.tar.gz"), summary.DocumentsArchive);
        await using var gzip = new GZipStream(File.OpenRead(summary.DocumentsArchive!), CompressionMode.Decompress);
        await using var reader = new TarReader(gzip);
        var entry = await reader.GetNextEntryAsync();
        while (entry is not null && !entry.Name.Replace('\\', '/').EndsWith("faturalar/f.pdf", StringComparison.Ordinal))
        {
            entry = await reader.GetNextEntryAsync();
        }

        Assert.NotNull(entry);
    }

    /// <summary>SQL Server hizmet hesabı (CI'da konteynerdeki mssql) da yazabilsin diye klasör herkese açık kurulur.</summary>
    private static string SharedFolder()
    {
        var root = Environment.GetEnvironmentVariable("HERYERDE_TEST_BACKUP_DIR") is { Length: > 0 } shared
            ? shared
            : Path.GetTempPath();
        var folder = Path.Combine(root, "heryerde-yedek-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(folder);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(folder, (UnixFileMode)0b111_111_111);
        }

        return folder;
    }
}
