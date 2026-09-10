namespace FeatureFlags.Api.Data.Entities;

public sealed class FeatureAllocationGroup
{
    public int Id { get; set; }
    public int FeatureFlagId { get; set; }
    public FeatureFlag FeatureFlag { get; set; } = null!;
    public required string VariantName { get; set; }
    public required string GroupName { get; set; }
}
