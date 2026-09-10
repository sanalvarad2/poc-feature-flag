using System.Text.Json;

namespace FeatureFlags.Api.Models;

public sealed record FeatureFilterDto(string Name, JsonElement? Parameters);

public sealed record FeatureResponse(
    string Name,
    bool Enabled,
    string RequirementType,
    IReadOnlyList<FeatureFilterDto> Filters,
    DateTimeOffset UpdatedAt);

public sealed record UpsertFeatureRequest(
    bool Enabled,
    string? RequirementType,
    IReadOnlyList<FeatureFilterDto>? Filters);

public sealed record FeatureEnabledResponse(string Name, bool Enabled);
