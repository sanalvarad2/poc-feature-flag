using System.Text.Json;

namespace FeatureFlags.Api.Models;

public sealed record FeatureFilterDto(string Name, JsonElement? Parameters);

public sealed record FeatureVariantDto(
    string Name,
    JsonElement? ConfigurationValue,
    string? StatusOverride);

public sealed record UserAllocationDto(string Variant, IReadOnlyList<string> Users);

public sealed record GroupAllocationDto(string Variant, IReadOnlyList<string> Groups);

public sealed record PercentileAllocationDto(string Variant, double From, double To);

public sealed record AllocationDto(
    string? DefaultWhenEnabled,
    string? DefaultWhenDisabled,
    string? Seed,
    IReadOnlyList<UserAllocationDto>? User,
    IReadOnlyList<GroupAllocationDto>? Group,
    IReadOnlyList<PercentileAllocationDto>? Percentile);

public sealed record FeatureResponse(
    string Name,
    bool Enabled,
    string RequirementType,
    IReadOnlyList<FeatureFilterDto> Filters,
    IReadOnlyList<FeatureVariantDto> Variants,
    AllocationDto? Allocation,
    DateTimeOffset UpdatedAt);

public sealed record UpsertFeatureRequest(
    bool Enabled,
    string? RequirementType,
    IReadOnlyList<FeatureFilterDto>? Filters,
    IReadOnlyList<FeatureVariantDto>? Variants,
    AllocationDto? Allocation);

public sealed record FeatureEnabledResponse(string Name, bool Enabled);

public sealed record FeatureVariantResponse(string Name, string? Variant, JsonElement? Value);
