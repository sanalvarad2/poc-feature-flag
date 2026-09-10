using System.Text.Json;
using FeatureFlags.Api.Models;
using FeatureFlags.Api.Services;
using Microsoft.FeatureManagement;

namespace FeatureFlags.Api.Endpoints;

public static class FeatureEndpoints
{
    public static RouteGroupBuilder MapFeatureEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/features").WithTags("Features");

        group.MapGet("/", async (FeatureAdminService admin, CancellationToken cancellationToken) =>
        {
            var features = await admin.ListAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(features);
        });

        group.MapGet("/{name}", async (string name, FeatureAdminService admin, CancellationToken cancellationToken) =>
        {
            var feature = await admin.GetAsync(name, cancellationToken).ConfigureAwait(false);
            return feature is null ? Results.NotFound() : Results.Ok(feature);
        });

        group.MapPost("/{name}", async (
            string name,
            UpsertFeatureRequest request,
            FeatureAdminService admin,
            CancellationToken cancellationToken) =>
        {
            var (feature, error, statusCode) = await admin.CreateAsync(name, request, cancellationToken)
                .ConfigureAwait(false);

            return statusCode switch
            {
                StatusCodes.Status201Created => Results.Created($"/api/features/{Uri.EscapeDataString(feature!.Name)}", feature),
                StatusCodes.Status409Conflict => Results.Conflict(new { error }),
                _ => Results.BadRequest(new { error })
            };
        });

        group.MapPut("/{name}", async (
            string name,
            UpsertFeatureRequest request,
            FeatureAdminService admin,
            CancellationToken cancellationToken) =>
        {
            var (feature, error, statusCode) = await admin.UpdateAsync(name, request, cancellationToken)
                .ConfigureAwait(false);

            return statusCode switch
            {
                StatusCodes.Status200OK => Results.Ok(feature),
                StatusCodes.Status404NotFound => Results.NotFound(new { error }),
                _ => Results.BadRequest(new { error })
            };
        });

        group.MapDelete("/{name}", async (string name, FeatureAdminService admin, CancellationToken cancellationToken) =>
        {
            var (deleted, error, statusCode) = await admin.DeleteAsync(name, cancellationToken).ConfigureAwait(false);
            return statusCode switch
            {
                StatusCodes.Status204NoContent when deleted => Results.NoContent(),
                StatusCodes.Status404NotFound => Results.NotFound(new { error }),
                _ => Results.BadRequest(new { error })
            };
        });

        group.MapGet("/{name}/enabled", async (
            string name,
            IFeatureManager featureManager,
            CancellationToken cancellationToken) =>
        {
            var enabled = await featureManager.IsEnabledAsync(name).ConfigureAwait(false);
            return Results.Ok(new FeatureEnabledResponse(name, enabled));
        });

        group.MapGet("/{name}/variant", async (
            string name,
            IVariantFeatureManager variantFeatureManager,
            FeatureAdminService admin,
            CancellationToken cancellationToken) =>
        {
            var feature = await admin.GetAsync(name, cancellationToken).ConfigureAwait(false);
            if (feature is null)
            {
                return Results.NotFound(new { error = $"Feature '{name}' was not found." });
            }

            var assigned = await variantFeatureManager.GetVariantAsync(name, cancellationToken).ConfigureAwait(false);
            JsonElement? value = null;
            if (assigned?.Name is not null)
            {
                value = feature.Variants
                    .FirstOrDefault(v => string.Equals(v.Name, assigned.Name, StringComparison.OrdinalIgnoreCase))
                    ?.ConfigurationValue;
            }

            return Results.Ok(new FeatureVariantResponse(name, assigned?.Name, value));
        });

        return group;
    }
}
