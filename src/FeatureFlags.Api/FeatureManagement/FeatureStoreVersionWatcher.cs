using FeatureFlags.Api.Data;
using FeatureFlags.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FeatureFlags.Api.FeatureManagement;

public sealed class FeatureStoreVersionWatcher(
    IServiceScopeFactory scopeFactory,
    SqlFeatureDefinitionProvider definitionProvider,
    IOptions<FeatureStoreOptions> options,
    ILogger<FeatureStoreVersionWatcher> logger) : BackgroundService
{
    private long _lastSeenVersion = -1;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.VersionPollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to poll feature store version.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FeatureFlagsDbContext>();
        var version = await db.FeatureStoreMeta
            .AsNoTracking()
            .Where(m => m.Id == 1)
            .Select(m => m.StoreVersion)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        if (_lastSeenVersion < 0)
        {
            _lastSeenVersion = version;
            return;
        }

        if (version > _lastSeenVersion)
        {
            logger.LogInformation(
                "Feature store version changed from {OldVersion} to {NewVersion}; invalidating definition cache.",
                _lastSeenVersion,
                version);
            definitionProvider.Invalidate();
            _lastSeenVersion = version;
        }
    }
}
