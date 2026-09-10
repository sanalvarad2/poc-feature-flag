using Microsoft.FeatureManagement.FeatureFilters;

namespace FeatureFlags.Api.FeatureManagement;

/// <summary>
/// POC targeting from HTTP headers: X-User-Id and X-Groups (comma-separated).
/// </summary>
public sealed class HttpHeaderTargetingContextAccessor(IHttpContextAccessor httpContextAccessor)
    : ITargetingContextAccessor
{
    public const string UserIdHeader = "X-User-Id";
    public const string GroupsHeader = "X-Groups";

    public ValueTask<TargetingContext> GetContextAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        var userId = httpContext?.Request.Headers[UserIdHeader].FirstOrDefault() ?? string.Empty;
        var groupsHeader = httpContext?.Request.Headers[GroupsHeader].FirstOrDefault();
        var groups = string.IsNullOrWhiteSpace(groupsHeader)
            ? Array.Empty<string>()
            : groupsHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return ValueTask.FromResult(new TargetingContext
        {
            UserId = userId,
            Groups = groups
        });
    }
}
