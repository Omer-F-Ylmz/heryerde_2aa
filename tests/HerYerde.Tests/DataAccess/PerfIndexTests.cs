using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.DataAccess;

/// <summary>Yönetim listelerinin taradığı kolonlarda indeks var mı.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class PerfIndexTests
{
    private static IEnumerable<string?> IndexNames(string table)
    {
        using var context = TestDb.NewContext();
        return context.Model
            .GetEntityTypes()
            .Where(e => e.GetTableName() == table)
            .SelectMany(e => e.GetIndexes())
            .Select(i => i.GetDatabaseName())
            .ToList();
    }

    [Fact]
    public void Siparis_tablosunda_durum_ve_tarih_indeksi_vardir()
        => Assert.Contains("ix_order_status_created_at", IndexNames("order"));

    [Fact]
    public void Urun_tablosunda_kategori_yayin_tarih_indeksi_vardir()
        => Assert.Contains("ix_product_category_id_is_active_created_at", IndexNames("product"));

    [Fact]
    public async Task Indeksler_veritabanina_uygulanmistir()
    {
        await TestDb.ResetAsync();
        await using var context = TestDb.NewContext();
        var applied = await context.Database
            .SqlQuery<string>($"SELECT name AS Value FROM sys.indexes WHERE name IN ('ix_order_status_created_at', 'ix_product_category_id_is_active_created_at')")
            .ToListAsync();

        Assert.Equal(2, applied.Count);
    }
}
