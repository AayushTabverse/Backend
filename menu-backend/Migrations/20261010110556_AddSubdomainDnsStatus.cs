using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSubdomainDnsStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubdomainDnsError",
                table: "Tenants",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubdomainDnsStatus",
                table: "Tenants",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubdomainDnsUpdatedAt",
                table: "Tenants",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubdomainDnsError",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SubdomainDnsStatus",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SubdomainDnsUpdatedAt",
                table: "Tenants");
        }
    }
}
