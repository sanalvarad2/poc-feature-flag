namespace FeatureFlags.Api.Options;

public sealed class FeatureStoreOptions
{
    public const string SectionName = "FeatureStore";

    public int VersionPollIntervalSeconds { get; set; } = 2;
}
