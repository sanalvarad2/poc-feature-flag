namespace FeatureFlags.Api.Data.Entities;

public sealed class FeatureFlag
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool Enabled { get; set; }
    public string RequirementType { get; set; } = "Any";
    public string? DefaultWhenEnabled { get; set; }
    public string? DefaultWhenDisabled { get; set; }
    public string? AllocationSeed { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<FeatureFilter> Filters { get; set; } = [];
    public List<FeatureVariant> Variants { get; set; } = [];
    public List<FeatureAllocationUser> AllocationUsers { get; set; } = [];
    public List<FeatureAllocationGroup> AllocationGroups { get; set; } = [];
    public List<FeatureAllocationPercentile> AllocationPercentiles { get; set; } = [];
}
