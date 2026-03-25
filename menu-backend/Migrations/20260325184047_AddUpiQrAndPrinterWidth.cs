using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddUpiQrAndPrinterWidth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrinterWidth",
                table: "Tenants",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpiQrCodeUrl",
                table: "Tenants",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrinterWidth",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "UpiQrCodeUrl",
                table: "Tenants");
        }
    }
}
