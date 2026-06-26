using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskSorter.Backend.Data;

#nullable disable

namespace TaskSorter.Backend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260625140000_AddGitHubCacheEntries")]
public partial class AddGitHubCacheEntries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GitHubCacheEntries",
            columns: table => new
            {
                Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                Operation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Target = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                ValueJson = table.Column<string>(type: "text", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GitHubCacheEntries", x => x.Key);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GitHubCacheEntries_ExpiresAt",
            table: "GitHubCacheEntries",
            column: "ExpiresAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "GitHubCacheEntries");
    }
}
