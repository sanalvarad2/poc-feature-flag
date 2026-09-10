using System.Text.Json;
using FeatureFlags.Api.Data.Entities;
using FeatureFlags.Api.FeatureManagement;
using Microsoft.FeatureManagement;

namespace FeatureFlags.Api.Tests;

public sealed class FeatureDefinitionMapperTests
{
    [Fact]
    public void Disabled_flag_maps_to_disabled_status()
    {
        var flag = new FeatureFlag
        {
            Name = "OffFeature",
            Enabled = false,
            RequirementType = "Any",
            Filters = [new FeatureFilter { Name = "Microsoft.Percentage", ParametersJson = """{"Value":50}""" }]
        };

        var definition = FeatureDefinitionMapper.ToDefinition(flag);

        Assert.Equal("OffFeature", definition.Name);
        Assert.Equal(FeatureStatus.Disabled, definition.Status);
        Assert.Empty(definition.EnabledFor);
    }

    [Fact]
    public void Enabled_flag_without_filters_maps_to_always_on()
    {
        var flag = new FeatureFlag
        {
            Name = "OnFeature",
            Enabled = true,
            RequirementType = "Any",
            Filters = []
        };

        var definition = FeatureDefinitionMapper.ToDefinition(flag);

        Assert.Equal(FeatureStatus.Conditional, definition.Status);
        Assert.Single(definition.EnabledFor);
        Assert.Equal("AlwaysOn", definition.EnabledFor.First().Name);
    }

    [Fact]
    public void Enabled_flag_with_filters_preserves_requirement_type_and_parameters()
    {
        var flag = new FeatureFlag
        {
            Name = "PercentFeature",
            Enabled = true,
            RequirementType = "All",
            Filters =
            [
                new FeatureFilter
                {
                    Name = "Microsoft.Percentage",
                    ParametersJson = """{"Value":25}""",
                    SortOrder = 0
                }
            ]
        };

        var definition = FeatureDefinitionMapper.ToDefinition(flag);

        Assert.Equal(RequirementType.All, definition.RequirementType);
        var filter = Assert.Single(definition.EnabledFor);
        Assert.Equal("Microsoft.Percentage", filter.Name);
        Assert.Equal("25", filter.Parameters["Value"]);
    }

    [Fact]
    public void BuildParameters_rejects_non_object_json()
    {
        Assert.Throws<InvalidOperationException>(() => FeatureDefinitionMapper.BuildParametersObject("[1,2]"));
    }

    [Fact]
    public void Maps_variants_allocation_and_status_override()
    {
        var flag = new FeatureFlag
        {
            Name = "Layout",
            Enabled = true,
            RequirementType = "Any",
            DefaultWhenEnabled = "Small",
            DefaultWhenDisabled = "Small",
            AllocationSeed = "seed-1",
            Variants =
            [
                new FeatureVariant
                {
                    Name = "Small",
                    ConfigurationJson = """{"Size":300}""",
                    StatusOverride = "None"
                },
                new FeatureVariant
                {
                    Name = "Big",
                    ConfigurationJson = """{"Size":500}""",
                    StatusOverride = "Disabled"
                }
            ],
            AllocationUsers = [new FeatureAllocationUser { VariantName = "Big", UserId = "alice" }],
            AllocationGroups = [new FeatureAllocationGroup { VariantName = "Big", GroupName = "Ring1" }],
            AllocationPercentiles =
            [
                new FeatureAllocationPercentile { VariantName = "Big", From = 0, To = 10 }
            ]
        };

        var definition = FeatureDefinitionMapper.ToDefinition(flag);

        Assert.Equal(2, definition.Variants.Count());
        var big = definition.Variants.Single(v => v.Name == "Big");
        Assert.Equal(StatusOverride.Disabled, big.StatusOverride);
        Assert.Equal("500", big.ConfigurationValue["Size"]);
        Assert.NotNull(definition.Allocation);
        Assert.Equal("Small", definition.Allocation!.DefaultWhenEnabled);
        Assert.Contains(definition.Allocation.User!, u => u.Variant == "Big" && u.Users.Contains("alice"));
        Assert.Contains(definition.Allocation.Percentile!, p => p.Variant == "Big" && p.From == 0 && p.To == 10);
    }
}
