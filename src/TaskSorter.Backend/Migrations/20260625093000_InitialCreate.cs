using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskSorter.Backend.Data;

#nullable disable

namespace TaskSorter.Backend.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260625093000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TaskProfiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                RepositoryLines = table.Column<string>(type: "text", nullable: false),
                LabelLines = table.Column<string>(type: "text", nullable: false),
                TaskLimit = table.Column<int>(type: "integer", nullable: false),
                DelayInMilliseconds = table.Column<int>(type: "integer", nullable: false),
                EncryptedGitHubToken = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaskProfiles", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TaskProfiles_Name",
            table: "TaskProfiles",
            column: "Name",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TaskProfiles");
    }
}
