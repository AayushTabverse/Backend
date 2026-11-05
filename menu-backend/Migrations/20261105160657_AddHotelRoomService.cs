using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddHotelRoomService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HotelModeEnabled",
                table: "Tenants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ChargeToRoom",
                table: "Orders",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "Orders",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DispatchedAt",
                table: "Orders",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "Orders",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StayId",
                table: "Orders",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    RoomNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Floor = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    RoomType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Stays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    RoomId = table.Column<Guid>(type: "char(36)", nullable: false),
                    GuestName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    GuestMobile = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Pin = table.Column<string>(type: "varchar(6)", maxLength: 6, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CheckInAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpectedCheckOutAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CheckedOutAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    BillNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stays_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StayId",
                table: "Orders",
                column: "StayId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_tenant_id_RoomNumber",
                table: "Rooms",
                columns: new[] { "tenant_id", "RoomNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Stays_RoomId",
                table: "Stays",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Stays_tenant_id_Status",
                table: "Stays",
                columns: new[] { "tenant_id", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Stays");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Orders_StayId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "HotelModeEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ChargeToRoom",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DispatchedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StayId",
                table: "Orders");
        }
    }
}
