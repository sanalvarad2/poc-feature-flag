namespace FeatureFlags.Api.Data.Entities;

public sealed class FeatureVariant
{
    public int Id { get; set; }
    public int FeatureFlagId { get; set; }
    public FeatureFlag FeatureFlag { get; set; } = null!;
    public required string Name { get; set; }
    /// <summary>Raw JSON token for configuration_value (object, array, string, number, bool, or null).</summary>
    public string? ConfigurationJson { get; set; }
    public string StatusOverride { get; set; } = "None";
}
