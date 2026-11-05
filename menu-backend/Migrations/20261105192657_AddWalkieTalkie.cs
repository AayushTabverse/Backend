using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace menu_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddWalkieTalkie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VoiceMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    SenderName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    SenderRole = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Channel = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    RecipientName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    QuickKey = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    QuickTable = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Audio = table.Column<byte[]>(type: "mediumblob", nullable: true),
                    AudioMime = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoiceMessages", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_VoiceMessages_tenant_id_CreatedAt",
                table: "VoiceMessages",
                columns: new[] { "tenant_id", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VoiceMessages");
        }
    }
}
