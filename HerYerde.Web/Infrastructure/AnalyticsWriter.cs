using HerYerde.Business.Abstract;
using HerYerde.Web.Models;

namespace HerYerde.Web.Infrastructure;

/// <summary>Biriken analitik kaydını tek SaveChanges ile yazar ve saklama süresi dolan ham kaydı günlük özete toplar.</summary>
public sealed class AnalyticsWriter(AnalyticsRecorder recorder, IServiceScopeFactory scopeFactory, TimeProvider clock)
{
    public async Task<int> FlushAsync(CancellationToken cancellationToken = default)
    {
        var batch = recorder.Drain();
        if (batch.Count == 0)
        {
            return 0;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAnalyticsService>().RecordAsync(batch, cancellationToken);
        return batch.Count;
    }

    public async Task<int> RollupAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAnalyticsService>().RollupAsync(IstanbulTime.Today(clock), cancellationToken);
    }
}
