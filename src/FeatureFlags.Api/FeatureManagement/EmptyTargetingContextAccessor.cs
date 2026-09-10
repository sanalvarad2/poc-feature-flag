using Microsoft.FeatureManagement.FeatureFilters;

namespace FeatureFlags.Api.FeatureManagement;

/// <summary>
/// POC targeting accessor with no user/group context.
/// </summary>
public sealed class EmptyTargetingContextAccessor : ITargetingContextAccessor
{
    public ValueTask<TargetingContext> GetContextAsync() =>
        ValueTask.FromResult(new TargetingContext
        {
            UserId = string.Empty,
            Groups = Array.Empty<string>()
        });
}
