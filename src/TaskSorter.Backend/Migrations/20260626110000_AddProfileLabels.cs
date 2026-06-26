using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskSorter.Backend.Data;

#nullable disable

namespace TaskSorter.Backend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260626110000_AddProfileLabels")]
public partial class AddProfileLabels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProfileLabels",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                OrderNumber = table.Column<int>(type: "integer", nullable: true),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                IsIgnored = table.Column<bool>(type: "boolean", nullable: false),
                FirstDiscoveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastDiscoveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProfileLabels", x => x.Id);
                table.ForeignKey("FK_ProfileLabels_TaskProfiles_ProfileId", x => x.ProfileId, "TaskProfiles", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_ProfileLabels_ProfileId_NormalizedName", "ProfileLabels", new[] { "ProfileId", "NormalizedName" }, unique: true);
        migrationBuilder.CreateIndex("IX_ProfileLabels_ProfileId_OrderNumber_SortOrder", "ProfileLabels", new[] { "ProfileId", "OrderNumber", "SortOrder" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "ProfileLabels");
}
