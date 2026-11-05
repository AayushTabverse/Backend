using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountingJson",
                table: "Tenants",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gstin",
                table: "Tenants",
                type: "varchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BillPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    BillNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    AccountKey = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    CustomerDueId = table.Column<Guid>(type: "char(36)", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RecordedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillPayments", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PurchaseBills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    SupplierName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    SupplierGstin = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    ExpenseLedger = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    TaxableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Cgst = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Sgst = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Igst = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAccountKey = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RecordedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseBills", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BillPayments_tenant_id_BillNumber",
                table: "BillPayments",
                columns: new[] { "tenant_id", "BillNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_BillPayments_tenant_id_PaidAt",
                table: "BillPayments",
                columns: new[] { "tenant_id", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseBills_tenant_id_InvoiceDate",
                table: "PurchaseBills",
                columns: new[] { "tenant_id", "InvoiceDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillPayments");

            migrationBuilder.DropTable(
                name: "PurchaseBills");

            migrationBuilder.DropColumn(
                name: "AccountingJson",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Gstin",
                table: "Tenants");
        }
    }
}
