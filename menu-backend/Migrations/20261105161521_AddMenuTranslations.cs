using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TranslationsJson",
                table: "MenuItems",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslationsJson",
                table: "MenuCategories",
                type: "longtext",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TranslationsJson",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "TranslationsJson",
                table: "MenuCategories");
        }
    }
}
