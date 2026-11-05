using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderingHoursAndGeofence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GeofenceRadiusMeters",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LastOrderMinutes",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Tenants",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Tenants",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OrderingOpenUntil",
                table: "Tenants",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QrOrderingHoursEnabled",
                table: "Tenants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeofenceRadiusMeters",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LastOrderMinutes",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OrderingOpenUntil",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "QrOrderingHoursEnabled",
                table: "Tenants");
        }
    }
}
