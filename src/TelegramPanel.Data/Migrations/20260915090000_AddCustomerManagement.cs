using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace TelegramPanel.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260915090000_AddCustomerManagement")]
public partial class AddCustomerManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
CREATE TABLE "CustomerGroups" ("Id" INTEGER NOT NULL CONSTRAINT "PK_CustomerGroups" PRIMARY KEY AUTOINCREMENT,"Name" TEXT NOT NULL,"Description" TEXT NULL,"CreatedAt" TEXT NOT NULL);
CREATE UNIQUE INDEX "IX_CustomerGroups_Name" ON "CustomerGroups" ("Name");
CREATE TABLE "CustomerImportBatches" ("Id" INTEGER NOT NULL CONSTRAINT "PK_CustomerImportBatches" PRIMARY KEY AUTOINCREMENT,"Name" TEXT NOT NULL,"SourceName" TEXT NULL,"Total" INTEGER NOT NULL,"Imported" INTEGER NOT NULL,"Duplicates" INTEGER NOT NULL,"Invalid" INTEGER NOT NULL,"CreatedAt" TEXT NOT NULL);
CREATE TABLE "Customers" ("Id" INTEGER NOT NULL CONSTRAINT "PK_Customers" PRIMARY KEY AUTOINCREMENT,"Phone" TEXT NULL,"Username" TEXT NULL,"TelegramUserId" INTEGER NULL,"AccessHash" INTEGER NULL,"DisplayName" TEXT NULL,"LookupStatus" TEXT NOT NULL,"InteractionStatus" TEXT NOT NULL,"Remark" TEXT NULL,"LastLookupAt" TEXT NULL,"LastInteractionAt" TEXT NULL,"CreatedAt" TEXT NOT NULL,"UpdatedAt" TEXT NOT NULL);
CREATE UNIQUE INDEX "IX_Customers_Phone" ON "Customers" ("Phone"); CREATE UNIQUE INDEX "IX_Customers_Username" ON "Customers" ("Username"); CREATE INDEX "IX_Customers_TelegramUserId" ON "Customers" ("TelegramUserId"); CREATE INDEX "IX_Customers_LookupStatus" ON "Customers" ("LookupStatus");
CREATE TABLE "CustomerBatchItems" ("CustomerImportBatchId" INTEGER NOT NULL,"CustomerId" INTEGER NOT NULL,"RawValue" TEXT NOT NULL,"CreatedAt" TEXT NOT NULL,CONSTRAINT "PK_CustomerBatchItems" PRIMARY KEY ("CustomerImportBatchId","CustomerId"),CONSTRAINT "FK_CustomerBatchItems_CustomerImportBatches_CustomerImportBatchId" FOREIGN KEY ("CustomerImportBatchId") REFERENCES "CustomerImportBatches" ("Id") ON DELETE CASCADE,CONSTRAINT "FK_CustomerBatchItems_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE);
CREATE INDEX "IX_CustomerBatchItems_CustomerId" ON "CustomerBatchItems" ("CustomerId");
CREATE TABLE "CustomerGroupAssignments" ("CustomerId" INTEGER NOT NULL,"CustomerGroupId" INTEGER NOT NULL,"CreatedAt" TEXT NOT NULL,CONSTRAINT "PK_CustomerGroupAssignments" PRIMARY KEY ("CustomerId","CustomerGroupId"),CONSTRAINT "FK_CustomerGroupAssignments_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE,CONSTRAINT "FK_CustomerGroupAssignments_CustomerGroups_CustomerGroupId" FOREIGN KEY ("CustomerGroupId") REFERENCES "CustomerGroups" ("Id") ON DELETE CASCADE);
CREATE INDEX "IX_CustomerGroupAssignments_CustomerGroupId" ON "CustomerGroupAssignments" ("CustomerGroupId");
""");
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
DROP TABLE IF EXISTS "CustomerBatchItems"; DROP TABLE IF EXISTS "CustomerGroupAssignments"; DROP TABLE IF EXISTS "CustomerImportBatches"; DROP TABLE IF EXISTS "CustomerGroups"; DROP TABLE IF EXISTS "Customers";
""");
}
