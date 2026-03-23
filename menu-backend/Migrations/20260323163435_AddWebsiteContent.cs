using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddWebsiteContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebsiteContents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    HeroTitle = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    HeroSubtitle = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    HeroImageUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    HeroCtaText = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    HeroCtaLink = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    AboutTitle = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    AboutDescription = table.Column<string>(type: "text", nullable: true),
                    AboutImageUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ChefName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    ChefImageUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ChefQuote = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Specialty1Title = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Specialty1Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    Specialty1Icon = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Specialty2Title = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Specialty2Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    Specialty2Icon = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Specialty3Title = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Specialty3Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    Specialty3Icon = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    GalleryImagesJson = table.Column<string>(type: "text", nullable: true),
                    TestimonialsJson = table.Column<string>(type: "text", nullable: true),
                    OperatingHoursJson = table.Column<string>(type: "text", nullable: true),
                    PrimaryColor = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    SecondaryColor = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    AccentColor = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    FontFamily = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    MetaTitle = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    MetaDescription = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    AnnouncementText = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    ShowAnnouncement = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsPublished = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebsiteContents", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebsiteContents");
        }
    }
}
