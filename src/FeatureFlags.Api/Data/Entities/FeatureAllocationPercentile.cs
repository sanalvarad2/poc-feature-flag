namespace FeatureFlags.Api.Data.Entities;

public sealed class FeatureAllocationPercentile
{
    public int Id { get; set; }
    public int FeatureFlagId { get; set; }
    public FeatureFlag FeatureFlag { get; set; } = null!;
    public required string VariantName { get; set; }
    /// <summary>Inclusive lower bound (library PercentileAllocation.From).</summary>
    public double From { get; set; }
    /// <summary>Exclusive upper bound (library PercentileAllocation.To).</summary>
    public double To { get; set; }
}
