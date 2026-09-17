using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramPanel.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260917120000_AddBatchTaskLogs")]
public partial class AddBatchTaskLogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BatchTaskLogs",
            columns: table => new
            {
                Id = table.Column<long>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                BatchTaskId = table.Column<int>(nullable: false),
                Level = table.Column<string>(maxLength: 20, nullable: false),
                Message = table.Column<string>(maxLength: 4000, nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BatchTaskLogs", x => x.Id);
            });
        migrationBuilder.CreateIndex("IX_BatchTaskLogs_BatchTaskId_CreatedAt", "BatchTaskLogs", new[] { "BatchTaskId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("BatchTaskLogs");
    }
}
