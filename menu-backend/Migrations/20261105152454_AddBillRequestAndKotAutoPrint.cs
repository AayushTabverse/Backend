using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddBillRequestAndKotAutoPrint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoPrintKot",
                table: "Tenants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "KotCopies",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "KotPrintTrigger",
                table: "Tenants",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "OnOrder");

            migrationBuilder.AddColumn<DateTime>(
                name: "BillRequestedAt",
                table: "Tables",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillRequestedBy",
                table: "Tables",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReprint",
                table: "PrintJobs",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoPrintKot",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "KotCopies",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "KotPrintTrigger",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BillRequestedAt",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "BillRequestedBy",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "IsReprint",
                table: "PrintJobs");
        }
    }
}
