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
    public async Task<IReadOnlyList<FeatureResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var flags = await QueryFlags(asNoTracking: true)
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

        var flag = new FeatureFlag
        {
            Name = name,
            Enabled = request.Enabled,
            RequirementType = FeatureDefinitionMapper.NormalizeRequirementType(request.RequirementType),
            UpdatedAt = DateTimeOffset.UtcNow,
            Filters = MapFilters(request.Filters),
            Variants = MapVariants(request.Variants),
            DefaultWhenEnabled = NullIfWhiteSpace(request.Allocation?.DefaultWhenEnabled),
            DefaultWhenDisabled = NullIfWhiteSpace(request.Allocation?.DefaultWhenDisabled),
            AllocationSeed = NullIfWhiteSpace(request.Allocation?.Seed),
            AllocationUsers = MapAllocationUsers(request.Allocation),
            AllocationGroups = MapAllocationGroups(request.Allocation),
            AllocationPercentiles = MapAllocationPercentiles(request.Allocation)
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
        flag.DefaultWhenEnabled = NullIfWhiteSpace(request.Allocation?.DefaultWhenEnabled);
        flag.DefaultWhenDisabled = NullIfWhiteSpace(request.Allocation?.DefaultWhenDisabled);
        flag.AllocationSeed = NullIfWhiteSpace(request.Allocation?.Seed);

        db.FeatureFilters.RemoveRange(flag.Filters);
        db.FeatureVariants.RemoveRange(flag.Variants);
        db.FeatureAllocationUsers.RemoveRange(flag.AllocationUsers);
        db.FeatureAllocationGroups.RemoveRange(flag.AllocationGroups);
        db.FeatureAllocationPercentiles.RemoveRange(flag.AllocationPercentiles);

        flag.Filters = MapFilters(request.Filters);
        flag.Variants = MapVariants(request.Variants);
        flag.AllocationUsers = MapAllocationUsers(request.Allocation);
        flag.AllocationGroups = MapAllocationGroups(request.Allocation);
        flag.AllocationPercentiles = MapAllocationPercentiles(request.Allocation);

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

    private IQueryable<FeatureFlag> QueryFlags(bool asNoTracking)
    {
        IQueryable<FeatureFlag> query = db.FeatureFlags
            .Include(f => f.Filters)
            .Include(f => f.Variants)
            .Include(f => f.AllocationUsers)
            .Include(f => f.AllocationGroups)
            .Include(f => f.AllocationPercentiles);

        return asNoTracking ? query.AsNoTracking() : query;
    }

    private async Task<FeatureFlag?> FindTrackedAsync(string name, CancellationToken cancellationToken, bool asNoTracking)
    {
        return await QueryFlags(asNoTracking)
            .FirstOrDefaultAsync(f => f.Name == name, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string? ValidateRequest(UpsertFeatureRequest request)
    {
        if (request.Filters is not null)
        {
            foreach (var filter in request.Filters)
            {
                if (string.IsNullOrWhiteSpace(filter.Name))
                {
                    return "Filter name is required.";
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
        }

        var variantNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (request.Variants is not null)
        {
            foreach (var variant in request.Variants)
            {
                if (string.IsNullOrWhiteSpace(variant.Name))
                {
                    return "Variant name is required.";
                }

                if (!variantNames.Add(variant.Name.Trim()))
                {
                    return $"Duplicate variant name '{variant.Name}'.";
                }

                var status = FeatureDefinitionMapper.NormalizeStatusOverride(variant.StatusOverride);
                if (variant.StatusOverride is not null
                    && !string.Equals(variant.StatusOverride, status, StringComparison.OrdinalIgnoreCase)
                    && !IsKnownStatusOverride(variant.StatusOverride))
                {
                    return $"Invalid statusOverride '{variant.StatusOverride}'. Use None, Enabled, or Disabled.";
                }
            }
        }

        if (request.Allocation is null)
        {
            return null;
        }

        return ValidateAllocation(request.Allocation, variantNames);
    }

    private static string? ValidateAllocation(AllocationDto allocation, HashSet<string> variantNames)
    {
        foreach (var referenced in EnumerateReferencedVariants(allocation))
        {
            if (!variantNames.Contains(referenced))
            {
                return $"Allocation references unknown variant '{referenced}'.";
            }
        }

        if (allocation.Percentile is not null)
        {
            foreach (var percentile in allocation.Percentile)
            {
                if (percentile.From < 0 || percentile.To > 100 || percentile.From >= percentile.To)
                {
                    return $"Invalid percentile range for variant '{percentile.Variant}' (From inclusive, To exclusive, 0-100).";
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateReferencedVariants(AllocationDto allocation)
    {
        if (!string.IsNullOrWhiteSpace(allocation.DefaultWhenEnabled))
        {
            yield return allocation.DefaultWhenEnabled.Trim();
        }

        if (!string.IsNullOrWhiteSpace(allocation.DefaultWhenDisabled))
        {
            yield return allocation.DefaultWhenDisabled.Trim();
        }

        if (allocation.User is not null)
        {
            foreach (var user in allocation.User)
            {
                yield return user.Variant;
            }
        }

        if (allocation.Group is not null)
        {
            foreach (var group in allocation.Group)
            {
                yield return group.Variant;
            }
        }

        if (allocation.Percentile is not null)
        {
            foreach (var percentile in allocation.Percentile)
            {
                yield return percentile.Variant;
            }
        }
    }

    private static bool IsKnownStatusOverride(string value) =>
        value.Equals("None", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Enabled", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Disabled", StringComparison.OrdinalIgnoreCase);

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
            ParametersJson = SerializeJsonObject(filter.Parameters),
            SortOrder = index
        }).ToList();
    }

    private static List<FeatureVariant> MapVariants(IReadOnlyList<FeatureVariantDto>? variants)
    {
        if (variants is null || variants.Count == 0)
        {
            return [];
        }

        return variants.Select(variant => new FeatureVariant
        {
            Name = variant.Name.Trim(),
            ConfigurationJson = SerializeConfigurationValue(variant.ConfigurationValue),
            StatusOverride = FeatureDefinitionMapper.NormalizeStatusOverride(variant.StatusOverride)
        }).ToList();
    }

    private static List<FeatureAllocationUser> MapAllocationUsers(AllocationDto? allocation)
    {
        if (allocation?.User is null)
        {
            return [];
        }

        return allocation.User
            .SelectMany(entry => entry.Users.Select(user => new FeatureAllocationUser
            {
                VariantName = entry.Variant.Trim(),
                UserId = user.Trim()
            }))
            .ToList();
    }

    private static List<FeatureAllocationGroup> MapAllocationGroups(AllocationDto? allocation)
    {
        if (allocation?.Group is null)
        {
            return [];
        }

        return allocation.Group
            .SelectMany(entry => entry.Groups.Select(group => new FeatureAllocationGroup
            {
                VariantName = entry.Variant.Trim(),
                GroupName = group.Trim()
            }))
            .ToList();
    }

    private static List<FeatureAllocationPercentile> MapAllocationPercentiles(AllocationDto? allocation)
    {
        if (allocation?.Percentile is null)
        {
            return [];
        }

        return allocation.Percentile.Select(entry => new FeatureAllocationPercentile
        {
            VariantName = entry.Variant.Trim(),
            From = entry.From,
            To = entry.To
        }).ToList();
    }

    private static string SerializeJsonObject(JsonElement? parameters)
    {
        if (parameters is null ||
            parameters.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return "{}";
        }

        return parameters.Value.GetRawText();
    }

    private static string? SerializeConfigurationValue(JsonElement? value)
    {
        if (value is null || value.Value.ValueKind is JsonValueKind.Undefined)
        {
            return null;
        }

        return value.Value.GetRawText();
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static FeatureResponse ToResponse(FeatureFlag flag)
    {
        var filters = flag.Filters
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .Select(f => new FeatureFilterDto(f.Name, ParseJsonElement(f.ParametersJson, "{}")))
            .ToList();

        var variants = flag.Variants
            .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .Select(v => new FeatureVariantDto(
                v.Name,
                ParseOptionalJsonElement(v.ConfigurationJson),
                v.StatusOverride))
            .ToList();

        AllocationDto? allocation = null;
        if (!string.IsNullOrWhiteSpace(flag.DefaultWhenEnabled)
            || !string.IsNullOrWhiteSpace(flag.DefaultWhenDisabled)
            || !string.IsNullOrWhiteSpace(flag.AllocationSeed)
            || flag.AllocationUsers.Count > 0
            || flag.AllocationGroups.Count > 0
            || flag.AllocationPercentiles.Count > 0)
        {
            allocation = new AllocationDto(
                flag.DefaultWhenEnabled,
                flag.DefaultWhenDisabled,
                flag.AllocationSeed,
                flag.AllocationUsers
                    .GroupBy(u => u.VariantName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new UserAllocationDto(g.Key, g.Select(x => x.UserId).ToList()))
                    .ToList(),
                flag.AllocationGroups
                    .GroupBy(u => u.VariantName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new GroupAllocationDto(g.Key, g.Select(x => x.GroupName).ToList()))
                    .ToList(),
                flag.AllocationPercentiles
                    .Select(p => new PercentileAllocationDto(p.VariantName, p.From, p.To))
                    .ToList());
        }

        return new FeatureResponse(
            flag.Name,
            flag.Enabled,
            flag.RequirementType,
            filters,
            variants,
            allocation,
            flag.UpdatedAt);
    }

    private static JsonElement ParseJsonElement(string? json, string fallback)
    {
        var payload = string.IsNullOrWhiteSpace(json) ? fallback : json;
        return JsonSerializer.Deserialize<JsonElement>(payload);
    }

    private static JsonElement? ParseOptionalJsonElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
