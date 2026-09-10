using FeatureFlags.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.FeatureManagement;

namespace FeatureFlags.Api.FeatureManagement;

public sealed class SqlFeatureDefinitionProvider(
    IServiceScopeFactory scopeFactory,
    IMemoryCache cache) : IFeatureDefinitionProvider
{
    public const string AllFeaturesCacheKey = "feature-definitions:all";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<FeatureDefinition?> GetFeatureDefinitionAsync(string featureName)
    {
        var all = await GetOrLoadAllAsync().ConfigureAwait(false);
        return all.TryGetValue(featureName, out var definition) ? definition : null;
    }

    public async IAsyncEnumerable<FeatureDefinition> GetAllFeatureDefinitionsAsync()
    {
        var all = await GetOrLoadAllAsync().ConfigureAwait(false);
        foreach (var definition in all.Values.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            yield return definition;
        }
    }

    public void Invalidate()
    {
        cache.Remove(AllFeaturesCacheKey);
    }

    private async Task<Dictionary<string, FeatureDefinition>> GetOrLoadAllAsync()
    {
        if (cache.TryGetValue(AllFeaturesCacheKey, out Dictionary<string, FeatureDefinition>? cached) && cached is not null)
        {
            return cached;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FeatureFlagsDbContext>();
        var flags = await db.FeatureFlags
            .AsNoTracking()
            .Include(f => f.Filters)
            .Include(f => f.Variants)
            .Include(f => f.AllocationUsers)
            .Include(f => f.AllocationGroups)
            .Include(f => f.AllocationPercentiles)
            .ToListAsync()
            .ConfigureAwait(false);

        var map = flags.ToDictionary(
            f => f.Name,
            FeatureDefinitionMapper.ToDefinition,
            StringComparer.OrdinalIgnoreCase);

        cache.Set(AllFeaturesCacheKey, map, CacheDuration);
        return map;
    }
}
