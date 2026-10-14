using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistSelfCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "WaitlistEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WaitlistNoShowMinutes",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<bool>(
                name: "WaitlistSelfCheckInEnabled",
                table: "Tenants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "WaitlistEntries");

            migrationBuilder.DropColumn(
                name: "WaitlistNoShowMinutes",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "WaitlistSelfCheckInEnabled",
                table: "Tenants");
        }
    }
}
