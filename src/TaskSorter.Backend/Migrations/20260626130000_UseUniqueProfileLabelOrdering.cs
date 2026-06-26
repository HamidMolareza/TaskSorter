using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskSorter.Backend.Data;

#nullable disable

namespace TaskSorter.Backend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260626130000_UseUniqueProfileLabelOrdering")]
public partial class UseUniqueProfileLabelOrdering : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProfileLabels_ProfileId_OrderNumber_SortOrder",
            table: "ProfileLabels");

        migrationBuilder.AlterColumn<int>(
            name: "SortOrder",
            table: "ProfileLabels",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.Sql("""
            WITH ranked_labels AS (
                SELECT "Id", ROW_NUMBER() OVER (
                    PARTITION BY "ProfileId"
                    ORDER BY "OrderNumber", "SortOrder", "Name") - 1 AS "NewSortOrder"
                FROM "ProfileLabels"
                WHERE NOT "IsIgnored" AND "OrderNumber" IS NOT NULL
            )
            UPDATE "ProfileLabels" AS labels
            SET "SortOrder" = ranked_labels."NewSortOrder"
            FROM ranked_labels
            WHERE labels."Id" = ranked_labels."Id";

            UPDATE "ProfileLabels"
            SET "SortOrder" = NULL
            WHERE "IsIgnored" OR "OrderNumber" IS NULL;
            """);

        migrationBuilder.DropColumn(
            name: "OrderNumber",
            table: "ProfileLabels");

        migrationBuilder.CreateIndex(
            name: "IX_ProfileLabels_ProfileId_SortOrder",
            table: "ProfileLabels",
            columns: new[] { "ProfileId", "SortOrder" },
            unique: true,
            filter: "\"SortOrder\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProfileLabels_ProfileId_SortOrder",
            table: "ProfileLabels");

        migrationBuilder.AddColumn<int>(
            name: "OrderNumber",
            table: "ProfileLabels",
            type: "integer",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE "ProfileLabels"
            SET "OrderNumber" = "SortOrder" + 1
            WHERE NOT "IsIgnored" AND "SortOrder" IS NOT NULL;

            UPDATE "ProfileLabels"
            SET "SortOrder" = 0
            WHERE "SortOrder" IS NULL;
            """);

        migrationBuilder.AlterColumn<int>(
            name: "SortOrder",
            table: "ProfileLabels",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProfileLabels_ProfileId_OrderNumber_SortOrder",
            table: "ProfileLabels",
            columns: new[] { "ProfileId", "OrderNumber", "SortOrder" });
    }
}
