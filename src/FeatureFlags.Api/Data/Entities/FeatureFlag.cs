namespace FeatureFlags.Api.Data.Entities;

public sealed class FeatureFlag
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool Enabled { get; set; }
    public string RequirementType { get; set; } = "Any";
    public DateTimeOffset UpdatedAt { get; set; }
    public List<FeatureFilter> Filters { get; set; } = [];
}
