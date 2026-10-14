using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WaitlistEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CustomerName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    CustomerMobile = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    PartySize = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    TokenNumber = table.Column<int>(type: "int", nullable: false),
                    QuotedWaitMinutes = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SeatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistEntries", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WaitlistEntryTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    WaitlistEntryId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TableId = table.Column<Guid>(type: "char(36)", nullable: false),
                    IsPrimary = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistEntryTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaitlistEntryTables_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WaitlistEntryTables_WaitlistEntries_WaitlistEntryId",
                        column: x => x.WaitlistEntryId,
                        principalTable: "WaitlistEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_tenant_id_Status_CreatedAt",
                table: "WaitlistEntries",
                columns: new[] { "tenant_id", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntryTables_TableId",
                table: "WaitlistEntryTables",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntryTables_tenant_id_TableId",
                table: "WaitlistEntryTables",
                columns: new[] { "tenant_id", "TableId" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntryTables_WaitlistEntryId",
                table: "WaitlistEntryTables",
                column: "WaitlistEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaitlistEntryTables");

            migrationBuilder.DropTable(
                name: "WaitlistEntries");
        }
    }
}
