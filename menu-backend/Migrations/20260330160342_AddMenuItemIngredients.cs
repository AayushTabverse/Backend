using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemIngredients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MenuItemIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "char(36)", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "char(36)", nullable: false),
                    QuantityUsed = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemIngredients_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemIngredients_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemIngredients_InventoryItemId",
                table: "MenuItemIngredients",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemIngredients_MenuItemId_InventoryItemId",
                table: "MenuItemIngredients",
                columns: new[] { "MenuItemId", "InventoryItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItemIngredients");
        }
    }
}
