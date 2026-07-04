using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskSorter.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryPriorityFactors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "RepositoryTiers",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "ProfileRepositories",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "RepositoryPriorityFactors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryPriorityFactors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryPriorityFactors_TaskProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "TaskProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryFactorRatings",
                columns: table => new
                {
                    ProfileRepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryPriorityFactorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryFactorRatings", x => new { x.ProfileRepositoryId, x.RepositoryPriorityFactorId });
                    table.ForeignKey(
                        name: "FK_RepositoryFactorRatings_ProfileRepositories_ProfileReposito~",
                        column: x => x.ProfileRepositoryId,
                        principalTable: "ProfileRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepositoryFactorRatings_RepositoryPriorityFactors_Repositor~",
                        column: x => x.RepositoryPriorityFactorId,
                        principalTable: "RepositoryPriorityFactors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryFactorRatings_RepositoryPriorityFactorId",
                table: "RepositoryFactorRatings",
                column: "RepositoryPriorityFactorId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryPriorityFactors_ProfileId_NormalizedName",
                table: "RepositoryPriorityFactors",
                columns: new[] { "ProfileId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryPriorityFactors_ProfileId_SortOrder",
                table: "RepositoryPriorityFactors",
                columns: new[] { "ProfileId", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryFactorRatings");

            migrationBuilder.DropTable(
                name: "RepositoryPriorityFactors");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RepositoryTiers");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProfileRepositories");
        }
    }
}
