using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CgstPercent",
                table: "Tenants",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceChargePercent",
                table: "Tenants",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SgstPercent",
                table: "Tenants",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CgstPercent",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ServiceChargePercent",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SgstPercent",
                table: "Tenants");
        }
    }
}
