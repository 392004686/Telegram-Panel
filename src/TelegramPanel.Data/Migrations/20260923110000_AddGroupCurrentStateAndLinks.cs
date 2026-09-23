using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramPanel.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260923110000_AddGroupCurrentStateAndLinks")]
public partial class AddGroupCurrentStateAndLinks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CurrentStatus",
            table: "Groups",
            type: "TEXT",
            maxLength: 100,
            nullable: false,
            defaultValue: "未知");

        migrationBuilder.AddColumn<int>(
            name: "CurrentStatusAccountId",
            table: "Groups",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CurrentStatusCheckedAtUtc",
            table: "Groups",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicLink",
            table: "Groups",
            type: "TEXT",
            maxLength: 512,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "InviteLink",
            table: "Groups",
            type: "TEXT",
            maxLength: 512,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CurrentStatus", table: "Groups");
        migrationBuilder.DropColumn(name: "CurrentStatusAccountId", table: "Groups");
        migrationBuilder.DropColumn(name: "CurrentStatusCheckedAtUtc", table: "Groups");
        migrationBuilder.DropColumn(name: "PublicLink", table: "Groups");
        migrationBuilder.DropColumn(name: "InviteLink", table: "Groups");
    }
}
