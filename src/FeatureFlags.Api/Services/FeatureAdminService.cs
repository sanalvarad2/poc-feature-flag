using System.Text.Json;
using FeatureFlags.Api.Data;
using FeatureFlags.Api.Data.Entities;
using FeatureFlags.Api.FeatureManagement;
using FeatureFlags.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FeatureFlags.Api.Services;

public sealed class FeatureAdminService(
    FeatureFlagsDbContext db,
    SqlFeatureDefinitionProvider definitionProvider)
{
    private static readonly HashSet<string> KnownFilterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.Percentage",
        "Microsoft.TimeWindow",
        "Microsoft.Targeting",
        "Percentage",
        "TimeWindow",
        "Targeting",
        "AlwaysOn"
    };

    public async Task<IReadOnlyList<FeatureResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var flags = await db.FeatureFlags
            .AsNoTracking()
            .Include(f => f.Filters)
            .OrderBy(f => f.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return flags.Select(ToResponse).ToList();
    }

    public async Task<FeatureResponse?> GetAsync(string name, CancellationToken cancellationToken)
    {
        var flag = await FindTrackedAsync(name, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return flag is null ? null : ToResponse(flag);
    }

    public async Task<(FeatureResponse? Feature, string? Error, int StatusCode)> CreateAsync(
        string name,
        UpsertFeatureRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (null, "Feature name is required.", StatusCodes.Status400BadRequest);
        }

        name = name.Trim();
        if (name.Contains(':', StringComparison.Ordinal))
        {
            return (null, "Feature name must not contain ':'.", StatusCodes.Status400BadRequest);
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return (null, validationError, StatusCodes.Status400BadRequest);
        }

        var exists = await db.FeatureFlags.AnyAsync(f => f.Name == name, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            return (null, $"Feature '{name}' already exists.", StatusCodes.Status409Conflict);
        }

        var now = DateTimeOffset.UtcNow;
        var flag = new FeatureFlag
        {
            Name = name,
            Enabled = request.Enabled,
            RequirementType = FeatureDefinitionMapper.NormalizeRequirementType(request.RequirementType),
            UpdatedAt = now,
            Filters = MapFilters(request.Filters)
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        db.FeatureFlags.Add(flag);
        await BumpStoreVersionAsync(cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        definitionProvider.Invalidate();
        return (ToResponse(flag), null, StatusCodes.Status201Created);
    }

    public async Task<(FeatureResponse? Feature, string? Error, int StatusCode)> UpdateAsync(
        string name,
        UpsertFeatureRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return (null, validationError, StatusCodes.Status400BadRequest);
        }

        var flag = await FindTrackedAsync(name, cancellationToken, asNoTracking: false).ConfigureAwait(false);
        if (flag is null)
        {
            return (null, $"Feature '{name}' was not found.", StatusCodes.Status404NotFound);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        flag.Enabled = request.Enabled;
        flag.RequirementType = FeatureDefinitionMapper.NormalizeRequirementType(request.RequirementType);
        flag.UpdatedAt = DateTimeOffset.UtcNow;

        db.FeatureFilters.RemoveRange(flag.Filters);
        flag.Filters = MapFilters(request.Filters);

        await BumpStoreVersionAsync(cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        definitionProvider.Invalidate();
        return (ToResponse(flag), null, StatusCodes.Status200OK);
    }

    public async Task<(bool Deleted, string? Error, int StatusCode)> DeleteAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var flag = await FindTrackedAsync(name, cancellationToken, asNoTracking: false).ConfigureAwait(false);
        if (flag is null)
        {
            return (false, $"Feature '{name}' was not found.", StatusCodes.Status404NotFound);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        db.FeatureFlags.Remove(flag);
        await BumpStoreVersionAsync(cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        definitionProvider.Invalidate();
        return (true, null, StatusCodes.Status204NoContent);
    }

    public async Task<long> GetStoreVersionAsync(CancellationToken cancellationToken)
    {
        return await db.FeatureStoreMeta
            .AsNoTracking()
            .Where(m => m.Id == 1)
            .Select(m => m.StoreVersion)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task BumpStoreVersionAsync(CancellationToken cancellationToken)
    {
        var meta = await db.FeatureStoreMeta.SingleAsync(m => m.Id == 1, cancellationToken).ConfigureAwait(false);
        meta.StoreVersion += 1;
    }

    private async Task<FeatureFlag?> FindTrackedAsync(string name, CancellationToken cancellationToken, bool asNoTracking)
    {
        IQueryable<FeatureFlag> query = db.FeatureFlags.Include(f => f.Filters);
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(f => f.Name == name, cancellationToken).ConfigureAwait(false);
    }

    private static string? ValidateRequest(UpsertFeatureRequest request)
    {
        if (request.Filters is null)
        {
            return null;
        }

        foreach (var filter in request.Filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Name))
            {
                return "Filter name is required.";
            }

            if (!KnownFilterNames.Contains(filter.Name) &&
                !filter.Name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
            {
                // Allow custom names but require JSON object parameters when present.
            }

            if (filter.Parameters is { ValueKind: not JsonValueKind.Object and not JsonValueKind.Undefined and not JsonValueKind.Null })
            {
                return $"Parameters for filter '{filter.Name}' must be a JSON object.";
            }

            if (IsPercentageFilter(filter.Name) && filter.Parameters is { ValueKind: JsonValueKind.Object } parameters)
            {
                if (!parameters.TryGetProperty("Value", out var value) ||
                    value.ValueKind is not (JsonValueKind.Number or JsonValueKind.String))
                {
                    return "Microsoft.Percentage requires a 'Value' parameter.";
                }
            }
        }

        return null;
    }

    private static bool IsPercentageFilter(string name) =>
        name.Equals("Microsoft.Percentage", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Percentage", StringComparison.OrdinalIgnoreCase);

    private static List<FeatureFilter> MapFilters(IReadOnlyList<FeatureFilterDto>? filters)
    {
        if (filters is null || filters.Count == 0)
        {
            return [];
        }

        return filters.Select((filter, index) => new FeatureFilter
        {
            Name = filter.Name.Trim(),
            ParametersJson = SerializeParameters(filter.Parameters),
            SortOrder = index
        }).ToList();
    }

    private static string SerializeParameters(JsonElement? parameters)
    {
        if (parameters is null ||
            parameters.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return "{}";
        }

        return parameters.Value.GetRawText();
    }

    private static FeatureResponse ToResponse(FeatureFlag flag)
    {
        var filters = flag.Filters
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .Select(f => new FeatureFilterDto(f.Name, ParseParametersElement(f.ParametersJson)))
            .ToList();

        return new FeatureResponse(
            flag.Name,
            flag.Enabled,
            flag.RequirementType,
            filters,
            flag.UpdatedAt);
    }

    private static JsonElement ParseParametersElement(string? parametersJson)
    {
        var json = string.IsNullOrWhiteSpace(parametersJson) ? "{}" : parametersJson;
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
