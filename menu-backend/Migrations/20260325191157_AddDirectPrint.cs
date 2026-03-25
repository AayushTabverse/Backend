using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectPrint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DirectPrint",
                table: "Tenants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DirectPrint",
                table: "Tenants");
        }
    }
}
