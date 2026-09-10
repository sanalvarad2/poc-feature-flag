using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeatureFlags.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveTablesToFeatureFlagsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "FeatureFlags");

            migrationBuilder.RenameTable(
                name: "FeatureStoreMeta",
                newName: "FeatureStoreMeta",
                newSchema: "FeatureFlags");

            migrationBuilder.RenameTable(
                name: "FeatureFlags",
                newName: "FeatureFlags",
                newSchema: "FeatureFlags");

            migrationBuilder.RenameTable(
                name: "FeatureFilters",
                newName: "FeatureFilters",
                newSchema: "FeatureFlags");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "FeatureStoreMeta",
                schema: "FeatureFlags",
                newName: "FeatureStoreMeta");

            migrationBuilder.RenameTable(
                name: "FeatureFlags",
                schema: "FeatureFlags",
                newName: "FeatureFlags");

            migrationBuilder.RenameTable(
                name: "FeatureFilters",
                schema: "FeatureFlags",
                newName: "FeatureFilters");
        }
    }
}
