using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskSorter.Backend.Data;

#nullable disable

namespace TaskSorter.Backend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260625160000_AddGitHubQuotaSnapshots")]
public partial class AddGitHubQuotaSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GitHubQuotaSnapshots",
            columns: table => new
            {
                TokenFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Resource = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Limit = table.Column<int>(type: "integer", nullable: false),
                Remaining = table.Column<int>(type: "integer", nullable: false),
                Used = table.Column<int>(type: "integer", nullable: false),
                ResetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GitHubQuotaSnapshots", x => new { x.TokenFingerprint, x.Resource });
            });

        migrationBuilder.CreateIndex(
            name: "IX_GitHubQuotaSnapshots_CapturedAt",
            table: "GitHubQuotaSnapshots",
            column: "CapturedAt");

        migrationBuilder.CreateIndex(
            name: "IX_GitHubQuotaSnapshots_ResetAt",
            table: "GitHubQuotaSnapshots",
            column: "ResetAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "GitHubQuotaSnapshots");
    }
}
