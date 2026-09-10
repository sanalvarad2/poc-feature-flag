using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FeatureFlags.Api.FeatureManagement;
using FeatureFlags.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FeatureFlags.Api.Tests;

public sealed class FeatureApiIntegrationTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"featureflags-tests-{Guid.NewGuid():N}.db");
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:FeatureStore", $"Data Source={_dbPath}");
            builder.UseSetting("FeatureStore:DatabaseProvider", "Sqlite");
            builder.UseSetting("FeatureStore:VersionPollIntervalSeconds", "1");
            builder.UseSetting("FeatureStore:ApplyMigrations", "true");
        });

        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch (IOException)
        {
            // SQLite may still hold the file briefly after host shutdown.
        }
    }

    [Fact]
    public async Task Admin_crud_and_probe_round_trip()
    {
        var create = await _client.PostAsJsonAsync("/api/features/CheckoutV2", new UpsertFeatureRequest(
            Enabled: true,
            RequirementType: "Any",
            Filters: null));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var duplicate = await _client.PostAsJsonAsync("/api/features/CheckoutV2", new UpsertFeatureRequest(
            Enabled: true,
            RequirementType: "Any",
            Filters: null));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var get = await _client.GetFromJsonAsync<FeatureResponse>("/api/features/CheckoutV2");
        Assert.NotNull(get);
        Assert.True(get.Enabled);

        var probeOn = await _client.GetFromJsonAsync<FeatureEnabledResponse>("/api/features/CheckoutV2/enabled");
        Assert.NotNull(probeOn);
        Assert.True(probeOn.Enabled);

        var update = await _client.PutAsJsonAsync("/api/features/CheckoutV2", new UpsertFeatureRequest(
            Enabled: false,
            RequirementType: "Any",
            Filters: null));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var probeOff = await _client.GetFromJsonAsync<FeatureEnabledResponse>("/api/features/CheckoutV2/enabled");
        Assert.NotNull(probeOff);
        Assert.False(probeOff.Enabled);

        var unknown = await _client.GetFromJsonAsync<FeatureEnabledResponse>("/api/features/DoesNotExist/enabled");
        Assert.NotNull(unknown);
        Assert.False(unknown.Enabled);

        var missing = await _client.GetAsync("/api/features/DoesNotExist");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var delete = await _client.DeleteAsync("/api/features/CheckoutV2");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var afterDelete = await _client.GetFromJsonAsync<FeatureEnabledResponse>("/api/features/CheckoutV2/enabled");
        Assert.NotNull(afterDelete);
        Assert.False(afterDelete.Enabled);
    }

    [Fact]
    public async Task Create_with_percentage_filter_persists_parameters()
    {
        using var doc = JsonDocument.Parse("""{"Value":40}""");
        var response = await _client.PostAsJsonAsync("/api/features/PartialRollout", new UpsertFeatureRequest(
            Enabled: true,
            RequirementType: "Any",
            Filters:
            [
                new FeatureFilterDto("Microsoft.Percentage", doc.RootElement.Clone())
            ]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var feature = await _client.GetFromJsonAsync<FeatureResponse>("/api/features/PartialRollout");
        Assert.NotNull(feature);
        var filter = Assert.Single(feature.Filters);
        Assert.Equal("Microsoft.Percentage", filter.Name);
        Assert.Equal(JsonValueKind.Object, filter.Parameters!.Value.ValueKind);
        Assert.Equal(40, filter.Parameters.Value.GetProperty("Value").GetInt32());
    }

    [Fact]
    public async Task Successful_write_bumps_store_version_and_local_cache_invalidates()
    {
        using var scope = _factory.Services.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<FeatureFlags.Api.Services.FeatureAdminService>();
        var provider = _factory.Services.GetRequiredService<SqlFeatureDefinitionProvider>();

        var before = await admin.GetStoreVersionAsync(CancellationToken.None);
        provider.Invalidate();

        var create = await _client.PostAsJsonAsync("/api/features/VersionedFlag", new UpsertFeatureRequest(
            Enabled: true,
            RequirementType: "Any",
            Filters: null));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var after = await admin.GetStoreVersionAsync(CancellationToken.None);
        Assert.True(after > before);

        var probe = await _client.GetFromJsonAsync<FeatureEnabledResponse>("/api/features/VersionedFlag/enabled");
        Assert.NotNull(probe);
        Assert.True(probe.Enabled);
    }

    [Fact]
    public async Task Version_watcher_invalidates_cache_when_store_version_changes()
    {
        var create = await _client.PostAsJsonAsync("/api/features/WatchedFlag", new UpsertFeatureRequest(
            Enabled: true,
            RequirementType: "Any",
            Filters: null));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        // Warm cache via probe
        var first = await _client.GetFromJsonAsync<FeatureEnabledResponse>("/api/features/WatchedFlag/enabled");
        Assert.True(first!.Enabled);

        // Simulate peer write: bump version and change row without going through local invalidate path
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FeatureFlags.Api.Data.FeatureFlagsDbContext>();
            var flag = db.FeatureFlags.Single(f => f.Name == "WatchedFlag");
            flag.Enabled = false;
            var meta = db.FeatureStoreMeta.Single(m => m.Id == 1);
            meta.StoreVersion += 1;
            await db.SaveChangesAsync();
            // Intentionally do NOT call provider.Invalidate() — watcher should clear cache
        }

        var provider = _factory.Services.GetRequiredService<SqlFeatureDefinitionProvider>();
        // Force a stale cache entry representing pre-update state is already warm from probe.
        // Wait for watcher poll (1s) plus margin.
        FeatureEnabledResponse? probe = null;
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(250);
            // Clear nothing; rely on watcher. Re-read via feature manager path.
            probe = await _client.GetFromJsonAsync<FeatureEnabledResponse>("/api/features/WatchedFlag/enabled");
            if (probe is { Enabled: false })
            {
                break;
            }
        }

        Assert.NotNull(probe);
        Assert.False(probe.Enabled);
        _ = provider; // keep provider referenced for clarity of intent
    }
}
