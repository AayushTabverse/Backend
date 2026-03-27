using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAiMarketingAndReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoogleReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    GoogleReviewId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    AuthorName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    ReviewText = table.Column<string>(type: "text", nullable: true),
                    ReviewCreateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReplyText = table.Column<string>(type: "text", nullable: true),
                    RepliedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Sentiment = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    SentimentThemesJson = table.Column<string>(type: "text", nullable: true),
                    AuthorProfileUrl = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleReviews", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MarketingPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Platform = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ContentText = table.Column<string>(type: "text", nullable: false),
                    HashtagsJson = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    SuggestedCaption = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CustomPrompt = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    FacebookPostId = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    InstagramPostId = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingPosts", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SocialMediaConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Platform = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    AccessToken = table.Column<string>(type: "text", nullable: true),
                    RefreshToken = table.Column<string>(type: "text", nullable: true),
                    PageId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    PageName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsConnected = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialMediaConnections", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleReviews_tenant_id_GoogleReviewId",
                table: "GoogleReviews",
                columns: new[] { "tenant_id", "GoogleReviewId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoogleReviews");

            migrationBuilder.DropTable(
                name: "MarketingPosts");

            migrationBuilder.DropTable(
                name: "SocialMediaConnections");
        }
    }
}
