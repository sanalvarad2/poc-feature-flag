using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeatureFlags.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeatureFlags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    RequirementType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureStoreMeta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureStoreMeta", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureFilters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeatureFlagId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFilters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeatureFilters_FeatureFlags_FeatureFlagId",
                        column: x => x.FeatureFlagId,
                        principalTable: "FeatureFlags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "FeatureStoreMeta",
                columns: new[] { "Id", "StoreVersion" },
                values: new object[] { 1, 0L });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFilters_FeatureFlagId",
                table: "FeatureFilters",
                column: "FeatureFlagId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_Name",
                table: "FeatureFlags",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureFilters");

            migrationBuilder.DropTable(
                name: "FeatureStoreMeta");

            migrationBuilder.DropTable(
                name: "FeatureFlags");
        }
    }
}
