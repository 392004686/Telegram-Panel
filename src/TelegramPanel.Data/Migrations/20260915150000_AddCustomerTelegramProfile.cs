using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace TelegramPanel.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260915150000_AddCustomerTelegramProfile")]
public partial class AddCustomerTelegramProfile : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("HasPhoto", "Customers", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("ActivityStatus", "Customers", maxLength: 40, nullable: false, defaultValue: "unknown");
        migrationBuilder.AddColumn<DateTime>("LastSeenAt", "Customers", nullable: true);
        migrationBuilder.AddColumn<bool>("IsPremium", "Customers", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("IsBot", "Customers", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("IsVerified", "Customers", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("IsScam", "Customers", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("IsFake", "Customers", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("IsDeleted", "Customers", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>("IsRestricted", "Customers", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("Birthday", "Customers", maxLength: 20, nullable: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
ALTER TABLE "Customers" DROP COLUMN "HasPhoto";
ALTER TABLE "Customers" DROP COLUMN "ActivityStatus";
ALTER TABLE "Customers" DROP COLUMN "LastSeenAt";
ALTER TABLE "Customers" DROP COLUMN "IsPremium";
ALTER TABLE "Customers" DROP COLUMN "IsBot";
ALTER TABLE "Customers" DROP COLUMN "IsVerified";
ALTER TABLE "Customers" DROP COLUMN "IsScam";
ALTER TABLE "Customers" DROP COLUMN "IsFake";
ALTER TABLE "Customers" DROP COLUMN "IsDeleted";
ALTER TABLE "Customers" DROP COLUMN "IsRestricted";
ALTER TABLE "Customers" DROP COLUMN "Birthday";
""");
    }
}
