using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomDomain",
                table: "Tenants",
                type: "varchar(253)",
                maxLength: 253,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomDomainActiveAt",
                table: "Tenants",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomDomainAddedAt",
                table: "Tenants",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomDomainToken",
                table: "Tenants",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomDomainVerifiedAt",
                table: "Tenants",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CustomDomain",
                table: "Tenants",
                column: "CustomDomain",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_CustomDomain",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CustomDomain",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CustomDomainActiveAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CustomDomainAddedAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CustomDomainToken",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CustomDomainVerifiedAt",
                table: "Tenants");
        }
    }
}
