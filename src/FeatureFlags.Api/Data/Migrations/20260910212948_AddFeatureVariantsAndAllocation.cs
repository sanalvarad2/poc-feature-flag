using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeatureFlags.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureVariantsAndAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllocationSeed",
                schema: "FeatureFlags",
                table: "FeatureFlags",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultWhenDisabled",
                schema: "FeatureFlags",
                table: "FeatureFlags",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultWhenEnabled",
                schema: "FeatureFlags",
                table: "FeatureFlags",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FeatureAllocationGroups",
                schema: "FeatureFlags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeatureFlagId = table.Column<int>(type: "int", nullable: false),
                    VariantName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureAllocationGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeatureAllocationGroups_FeatureFlags_FeatureFlagId",
                        column: x => x.FeatureFlagId,
                        principalSchema: "FeatureFlags",
                        principalTable: "FeatureFlags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeatureAllocationPercentiles",
                schema: "FeatureFlags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeatureFlagId = table.Column<int>(type: "int", nullable: false),
                    VariantName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    From = table.Column<double>(type: "float", nullable: false),
                    To = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureAllocationPercentiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeatureAllocationPercentiles_FeatureFlags_FeatureFlagId",
                        column: x => x.FeatureFlagId,
                        principalSchema: "FeatureFlags",
                        principalTable: "FeatureFlags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeatureAllocationUsers",
                schema: "FeatureFlags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeatureFlagId = table.Column<int>(type: "int", nullable: false),
                    VariantName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureAllocationUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeatureAllocationUsers_FeatureFlags_FeatureFlagId",
                        column: x => x.FeatureFlagId,
                        principalSchema: "FeatureFlags",
                        principalTable: "FeatureFlags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeatureVariants",
                schema: "FeatureFlags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeatureFlagId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatusOverride = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeatureVariants_FeatureFlags_FeatureFlagId",
                        column: x => x.FeatureFlagId,
                        principalSchema: "FeatureFlags",
                        principalTable: "FeatureFlags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureAllocationGroups_FeatureFlagId_VariantName_GroupName",
                schema: "FeatureFlags",
                table: "FeatureAllocationGroups",
                columns: new[] { "FeatureFlagId", "VariantName", "GroupName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureAllocationPercentiles_FeatureFlagId",
                schema: "FeatureFlags",
                table: "FeatureAllocationPercentiles",
                column: "FeatureFlagId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureAllocationUsers_FeatureFlagId_VariantName_UserId",
                schema: "FeatureFlags",
                table: "FeatureAllocationUsers",
                columns: new[] { "FeatureFlagId", "VariantName", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureVariants_FeatureFlagId_Name",
                schema: "FeatureFlags",
                table: "FeatureVariants",
                columns: new[] { "FeatureFlagId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureAllocationGroups",
                schema: "FeatureFlags");

            migrationBuilder.DropTable(
                name: "FeatureAllocationPercentiles",
                schema: "FeatureFlags");

            migrationBuilder.DropTable(
                name: "FeatureAllocationUsers",
                schema: "FeatureFlags");

            migrationBuilder.DropTable(
                name: "FeatureVariants",
                schema: "FeatureFlags");

            migrationBuilder.DropColumn(
                name: "AllocationSeed",
                schema: "FeatureFlags",
                table: "FeatureFlags");

            migrationBuilder.DropColumn(
                name: "DefaultWhenDisabled",
                schema: "FeatureFlags",
                table: "FeatureFlags");

            migrationBuilder.DropColumn(
                name: "DefaultWhenEnabled",
                schema: "FeatureFlags",
                table: "FeatureFlags");
        }
    }
}
