using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kinnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageNotifications",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChatMessageId = table.Column<int>(type: "integer", nullable: false),
                    FromUserId = table.Column<string>(type: "text", nullable: false),
                    ToUserId = table.Column<string>(type: "text", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    EmailSent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageNotifications_ChatMessages_ChatMessageId",
                        column: x => x.ChatMessageId,
                        principalSchema: "app",
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageNotifications_ChatMessageId",
                schema: "app",
                table: "MessageNotifications",
                column: "ChatMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageNotifications_IsRead_EmailSent_CreatedAtUtc",
                schema: "app",
                table: "MessageNotifications",
                columns: new[] { "IsRead", "EmailSent", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageNotifications_ToUserId_IsRead",
                schema: "app",
                table: "MessageNotifications",
                columns: new[] { "ToUserId", "IsRead" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageNotifications",
                schema: "app");
        }
    }
}
