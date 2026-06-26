using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskSorter.Backend.Data;

#nullable disable

namespace TaskSorter.Backend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260626090000_AddRepositoryTiersAndProfileRepositories")]
public partial class AddRepositoryTiersAndProfileRepositories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RepositoryTiers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                NormalizedName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Score = table.Column<int>(type: "integer", nullable: false),
                IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_RepositoryTiers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ProfileRepositories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                Owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                RepositoryTierId = table.Column<Guid>(type: "uuid", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProfileRepositories", x => x.Id);
                table.ForeignKey("FK_ProfileRepositories_RepositoryTiers_RepositoryTierId", x => x.RepositoryTierId, "RepositoryTiers", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProfileRepositories_TaskProfiles_ProfileId", x => x.ProfileId, "TaskProfiles", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_ProfileRepositories_ProfileId_Owner_Name", "ProfileRepositories", new[] { "ProfileId", "Owner", "Name" }, unique: true);
        migrationBuilder.CreateIndex("IX_ProfileRepositories_ProfileId_SortOrder", "ProfileRepositories", new[] { "ProfileId", "SortOrder" }, unique: true);
        migrationBuilder.CreateIndex("IX_ProfileRepositories_RepositoryTierId", "ProfileRepositories", "RepositoryTierId");
        migrationBuilder.CreateIndex("IX_RepositoryTiers_IsDefault", "RepositoryTiers", "IsDefault", unique: true, filter: "\"IsDefault\" = true");
        migrationBuilder.CreateIndex("IX_RepositoryTiers_NormalizedName", "RepositoryTiers", "NormalizedName", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ProfileRepositories");
        migrationBuilder.DropTable(name: "RepositoryTiers");
    }
}
