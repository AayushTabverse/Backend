using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationsAndTableMerge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OnlineReservationsEnabled",
                table: "Tenants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReservationGraceMinutes",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<Guid>(
                name: "MergedIntoTableId",
                table: "Tables",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CustomerName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    CustomerMobile = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    CustomerEmail = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    PartySize = table.Column<int>(type: "int", nullable: false),
                    ReservedFor = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    ResponseMessage = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SeatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    WaitlistEntryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReservationTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ReservationId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TableId = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservationTables_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReservationTables_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_MergedIntoTableId",
                table: "Tables",
                column: "MergedIntoTableId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_tenant_id_Status_ReservedFor",
                table: "Reservations",
                columns: new[] { "tenant_id", "Status", "ReservedFor" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationTables_ReservationId",
                table: "ReservationTables",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationTables_TableId",
                table: "ReservationTables",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationTables_tenant_id_TableId",
                table: "ReservationTables",
                columns: new[] { "tenant_id", "TableId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Tables_Tables_MergedIntoTableId",
                table: "Tables",
                column: "MergedIntoTableId",
                principalTable: "Tables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tables_Tables_MergedIntoTableId",
                table: "Tables");

            migrationBuilder.DropTable(
                name: "ReservationTables");

            migrationBuilder.DropTable(
                name: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Tables_MergedIntoTableId",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "OnlineReservationsEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ReservationGraceMinutes",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MergedIntoTableId",
                table: "Tables");
        }
    }
}
