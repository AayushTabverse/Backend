using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAndAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "MarketingPosts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CurrentQuantity = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Unit = table.Column<int>(type: "int", nullable: false),
                    MinimumQuantity = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CostPerUnit = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Supplier = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    SupplierContact = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    LastRestockedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WaiterTableAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    WaiterId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TableId = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaiterTableAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaiterTableAssignments_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WaiterTableAssignments_Users_WaiterId",
                        column: x => x.WaiterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InventoryLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "char(36)", nullable: false),
                    QuantityChange = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    QuantityAfter = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ChangeType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ChangedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryLogs_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_tenant_id_Name",
                table: "InventoryItems",
                columns: new[] { "tenant_id", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLogs_InventoryItemId",
                table: "InventoryLogs",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WaiterTableAssignments_TableId",
                table: "WaiterTableAssignments",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_WaiterTableAssignments_tenant_id_WaiterId_TableId",
                table: "WaiterTableAssignments",
                columns: new[] { "tenant_id", "WaiterId", "TableId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaiterTableAssignments_WaiterId",
                table: "WaiterTableAssignments",
                column: "WaiterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryLogs");

            migrationBuilder.DropTable(
                name: "WaiterTableAssignments");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "MarketingPosts",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
