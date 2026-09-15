using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramPanel.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260916100000_AddCustomerLookupPersistence")]
public partial class AddCustomerLookupPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Nickname", "Customers", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<DateTime>("LastDataSyncAt", "Customers", nullable: true);
        migrationBuilder.Sql("UPDATE Customers SET DisplayName = NULL WHERE lower(trim(DisplayName)) IN ('lookup contact', 'telegram lookup');");
        migrationBuilder.CreateTable("CustomerLookupBatches", table => new
        {
            Id = table.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true), BatchTaskId = table.Column<int>(nullable: true), Name = table.Column<string>(maxLength: 150, nullable: false), Mode = table.Column<string>(maxLength: 20, nullable: false), AccountSource = table.Column<string>(maxLength: 20, nullable: false), AccountIdsJson = table.Column<string>(nullable: false), AccountCategoryId = table.Column<int>(nullable: true), TargetOrder = table.Column<string>(maxLength: 20, nullable: false), MinDelaySeconds = table.Column<int>(nullable: false), MaxDelaySeconds = table.Column<int>(nullable: false), Status = table.Column<string>(maxLength: 30, nullable: false), Total = table.Column<int>(nullable: false), Completed = table.Column<int>(nullable: false), Found = table.Column<int>(nullable: false), NotFound = table.Column<int>(nullable: false), Failed = table.Column<int>(nullable: false), CreatedAt = table.Column<DateTime>(nullable: false), StartedAt = table.Column<DateTime>(nullable: true), CompletedAt = table.Column<DateTime>(nullable: true), LastHeartbeatAt = table.Column<DateTime>(nullable: true)
        }, constraints: table => table.PrimaryKey("PK_CustomerLookupBatches", x => x.Id));
        migrationBuilder.CreateTable("CustomerLookupItems", table => new
        {
            Id = table.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true), CustomerLookupBatchId = table.Column<int>(nullable: false), RawTarget = table.Column<string>(maxLength: 255, nullable: false), NormalizedTarget = table.Column<string>(maxLength: 255, nullable: false), Sequence = table.Column<int>(nullable: false), AccountId = table.Column<int>(nullable: true), CustomerId = table.Column<int>(nullable: true), ExistingCustomer = table.Column<bool>(nullable: false), Status = table.Column<string>(maxLength: 30, nullable: false), Error = table.Column<string>(maxLength: 2000, nullable: true), AttemptCount = table.Column<int>(nullable: false), TelegramUserId = table.Column<long>(nullable: true), AccessHash = table.Column<long>(nullable: true), Phone = table.Column<string>(maxLength: 32, nullable: true), Username = table.Column<string>(maxLength: 100, nullable: true), DisplayName = table.Column<string>(maxLength: 200, nullable: true), HasPhoto = table.Column<bool>(nullable: false), ActivityStatus = table.Column<string>(maxLength: 40, nullable: false), LastSeenAt = table.Column<DateTime>(nullable: true), IsPremium = table.Column<bool>(nullable: false), IsBot = table.Column<bool>(nullable: false), IsVerified = table.Column<bool>(nullable: false), IsScam = table.Column<bool>(nullable: false), IsFake = table.Column<bool>(nullable: false), IsDeleted = table.Column<bool>(nullable: false), IsRestricted = table.Column<bool>(nullable: false), Birthday = table.Column<string>(maxLength: 20, nullable: true), StartedAt = table.Column<DateTime>(nullable: true), CompletedAt = table.Column<DateTime>(nullable: true)
        }, constraints: table => { table.PrimaryKey("PK_CustomerLookupItems", x => x.Id); table.ForeignKey("FK_CustomerLookupItems_CustomerLookupBatches_CustomerLookupBatchId", x => x.CustomerLookupBatchId, "CustomerLookupBatches", "Id", onDelete: ReferentialAction.Cascade); });
        migrationBuilder.CreateIndex("IX_CustomerLookupBatches_BatchTaskId", "CustomerLookupBatches", "BatchTaskId", unique: true);
        migrationBuilder.CreateIndex("IX_CustomerLookupBatches_CreatedAt_Status", "CustomerLookupBatches", new[] { "CreatedAt", "Status" });
        migrationBuilder.CreateIndex("IX_CustomerLookupItems_CustomerLookupBatchId_Sequence", "CustomerLookupItems", new[] { "CustomerLookupBatchId", "Sequence" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("CustomerLookupItems");
        migrationBuilder.DropTable("CustomerLookupBatches");
        migrationBuilder.Sql("ALTER TABLE Customers DROP COLUMN Nickname; ALTER TABLE Customers DROP COLUMN LastDataSyncAt;");
    }
}
