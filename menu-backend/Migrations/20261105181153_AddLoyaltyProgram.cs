using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyProgram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LoyaltyBirthdayBonus",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyDailyEarnCap",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "LoyaltyEnabled",
                table: "Tenants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyExpiryMonths",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 12);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyMaxRedeemPercent",
                table: "Tenants",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 20m);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyMinRedeemPoints",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyPointValue",
                table: "Tenants",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyPointsPer100",
                table: "Tenants",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 5m);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyWelcomeBonus",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyDiscount",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Customers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastBirthdayBonusYear",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastVisitAt",
                table: "Customers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointsBalance",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalSpent",
                table: "Customers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "VisitCount",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LoyaltyTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CustomerId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    BillNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Note = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_tenant_id_CustomerMobile",
                table: "Customers",
                columns: new[] { "tenant_id", "CustomerMobile" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_CustomerId",
                table: "LoyaltyTransactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_tenant_id_BillNumber",
                table: "LoyaltyTransactions",
                columns: new[] { "tenant_id", "BillNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_tenant_id_CustomerId",
                table: "LoyaltyTransactions",
                columns: new[] { "tenant_id", "CustomerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoyaltyTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Customers_tenant_id_CustomerMobile",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LoyaltyBirthdayBonus",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LoyaltyDailyEarnCap",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LoyaltyEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LoyaltyExpiryMonths",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LoyaltyMaxRedeemPercent",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LoyaltyMinRedeemPoints",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LoyaltyPointValue",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LoyaltyPointsPer100",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LoyaltyWelcomeBonus",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LoyaltyDiscount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LastBirthdayBonusYear",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LastVisitAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PointsBalance",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TotalSpent",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "VisitCount",
                table: "Customers");
        }
    }
}
