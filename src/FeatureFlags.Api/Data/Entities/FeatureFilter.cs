namespace FeatureFlags.Api.Data.Entities;

public sealed class FeatureFilter
{
    public int Id { get; set; }
    public int FeatureFlagId { get; set; }
    public FeatureFlag FeatureFlag { get; set; } = null!;
    public required string Name { get; set; }
    public string ParametersJson { get; set; } = "{}";
    public int SortOrder { get; set; }
}
