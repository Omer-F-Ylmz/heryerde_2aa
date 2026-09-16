using HerYerde.Entities.Concrete;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Business;

/// <summary>D15 B4 / veri envanteri: arama günlüğü bir yıldan eskiyse gecelik temizlikte silinir.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class SearchLogPurgeTests : IAsyncLifetime
{
    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Bir_yildan_eski_arama_satiri_silinir()
    {
        await using (var context = TestDb.NewContext())
        {
            context.SearchLogs.AddRange(
                new SearchLog { Term = "eski", ResultCount = 0, CreatedAt = TestClock.Now.AddDays(-366) },
                new SearchLog { Term = "yeni", ResultCount = 2, CreatedAt = TestClock.Now.AddDays(-364) });
            await context.SaveChangesAsync();
        }

        int purged;
        await using (var job = TestDb.NewContext())
        {
            purged = await TestData.NewSearchLogManager(job).PurgeOlderThanAsync(TimeSpan.FromDays(365));
        }

        Assert.Equal(1, purged);
        await using var check = TestDb.NewContext();
        Assert.Equal("yeni", (await check.SearchLogs.SingleAsync()).Term);
    }
}
