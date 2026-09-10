using System.Text;
using System.Text.Json;
using FeatureFlags.Api.Data.Entities;
using Microsoft.FeatureManagement;

namespace FeatureFlags.Api.FeatureManagement;

public static class FeatureDefinitionMapper
{
    public static FeatureDefinition ToDefinition(FeatureFlag flag)
    {
        List<FeatureFilterConfiguration> filters;
        FeatureStatus status;

        if (!flag.Enabled)
        {
            status = FeatureStatus.Disabled;
            filters = [];
        }
        else
        {
            status = FeatureStatus.Conditional;
            filters = flag.Filters
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.Id)
                .Select(ToFilterConfiguration)
                .ToList();

            if (filters.Count == 0)
            {
                filters.Add(new FeatureFilterConfiguration { Name = "AlwaysOn" });
            }
        }

        return new FeatureDefinition
        {
            Name = flag.Name,
            Status = status,
            EnabledFor = filters,
            RequirementType = ParseRequirementType(flag.RequirementType),
            Variants = MapVariants(flag.Variants),
            Allocation = MapAllocation(flag)
        };
    }

    public static FeatureFilterConfiguration ToFilterConfiguration(FeatureFilter filter)
    {
        return new FeatureFilterConfiguration
        {
            Name = filter.Name,
            Parameters = BuildParametersObject(filter.ParametersJson)
        };
    }

    public static IReadOnlyList<VariantDefinition> MapVariants(IEnumerable<FeatureVariant> variants)
    {
        return variants
            .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .Select(v => new VariantDefinition
            {
                Name = v.Name,
                ConfigurationValue = BuildConfigurationValueSection(v.ConfigurationJson),
                StatusOverride = ParseStatusOverride(v.StatusOverride)
            })
            .ToList();
    }

    public static Allocation? MapAllocation(FeatureFlag flag)
    {
        var hasDefaults = !string.IsNullOrWhiteSpace(flag.DefaultWhenEnabled)
            || !string.IsNullOrWhiteSpace(flag.DefaultWhenDisabled)
            || !string.IsNullOrWhiteSpace(flag.AllocationSeed);
        var hasRows = flag.AllocationUsers.Count > 0
            || flag.AllocationGroups.Count > 0
            || flag.AllocationPercentiles.Count > 0;

        if (!hasDefaults && !hasRows)
        {
            return null;
        }

        return new Allocation
        {
            DefaultWhenEnabled = flag.DefaultWhenEnabled,
            DefaultWhenDisabled = flag.DefaultWhenDisabled,
            Seed = flag.AllocationSeed,
            User = flag.AllocationUsers
                .GroupBy(u => u.VariantName, StringComparer.OrdinalIgnoreCase)
                .Select(g => new UserAllocation
                {
                    Variant = g.Key,
                    Users = g.Select(x => x.UserId).ToList()
                })
                .ToList(),
            Group = flag.AllocationGroups
                .GroupBy(u => u.VariantName, StringComparer.OrdinalIgnoreCase)
                .Select(g => new GroupAllocation
                {
                    Variant = g.Key,
                    Groups = g.Select(x => x.GroupName).ToList()
                })
                .ToList(),
            Percentile = flag.AllocationPercentiles
                .Select(p => new PercentileAllocation
                {
                    Variant = p.VariantName,
                    From = p.From,
                    To = p.To
                })
                .ToList()
        };
    }

    public static IConfiguration BuildParametersObject(string? parametersJson)
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

    public static IConfigurationSection BuildConfigurationValueSection(string? configurationJson)
    {
        var token = string.IsNullOrWhiteSpace(configurationJson) ? "null" : configurationJson;
        var wrapped = $"{{\"configuration_value\":{token}}}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wrapped));
        var root = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();
        return root.GetSection("configuration_value");
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

    public static StatusOverride ParseStatusOverride(string? value)
    {
        if (string.Equals(value, nameof(StatusOverride.Enabled), StringComparison.OrdinalIgnoreCase))
        {
            return StatusOverride.Enabled;
        }

        if (string.Equals(value, nameof(StatusOverride.Disabled), StringComparison.OrdinalIgnoreCase))
        {
            return StatusOverride.Disabled;
        }

        return StatusOverride.None;
    }

    public static string NormalizeStatusOverride(string? value)
    {
        return ParseStatusOverride(value).ToString();
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
