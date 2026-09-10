using System.Text;
using System.Text.Json;
using FeatureFlags.Api.Data.Entities;
using Microsoft.FeatureManagement;

namespace FeatureFlags.Api.FeatureManagement;

public static class FeatureDefinitionMapper
{
    public static FeatureDefinition ToDefinition(FeatureFlag flag)
    {
        if (!flag.Enabled)
        {
            return new FeatureDefinition
            {
                Name = flag.Name,
                Status = FeatureStatus.Disabled,
                EnabledFor = Array.Empty<FeatureFilterConfiguration>(),
                RequirementType = ParseRequirementType(flag.RequirementType)
            };
        }

        var filters = flag.Filters
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .Select(ToFilterConfiguration)
            .ToList();

        if (filters.Count == 0)
        {
            filters.Add(new FeatureFilterConfiguration { Name = "AlwaysOn" });
        }

        return new FeatureDefinition
        {
            Name = flag.Name,
            Status = FeatureStatus.Conditional,
            EnabledFor = filters,
            RequirementType = ParseRequirementType(flag.RequirementType)
        };
    }

    public static FeatureFilterConfiguration ToFilterConfiguration(FeatureFilter filter)
    {
        return new FeatureFilterConfiguration
        {
            Name = filter.Name,
            Parameters = BuildParameters(filter.ParametersJson)
        };
    }

    public static IConfiguration BuildParameters(string? parametersJson)
    {
        var json = string.IsNullOrWhiteSpace(parametersJson) ? "{}" : parametersJson;
        if (!IsJsonObject(json))
        {
            throw new InvalidOperationException("Filter parameters must be a JSON object.");
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();
    }

    public static RequirementType ParseRequirementType(string? value)
    {
        if (string.Equals(value, "All", StringComparison.OrdinalIgnoreCase))
        {
            return RequirementType.All;
        }

        return RequirementType.Any;
    }

    public static string NormalizeRequirementType(string? value)
    {
        return ParseRequirementType(value) == RequirementType.All ? "All" : "Any";
    }

    private static bool IsJsonObject(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
