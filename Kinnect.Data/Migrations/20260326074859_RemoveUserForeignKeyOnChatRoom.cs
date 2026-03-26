using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserForeignKeyOnChatRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatRooms_AspNetUsers_AdminUserId",
                schema: "app",
                table: "ChatRooms");

            migrationBuilder.DropIndex(
                name: "IX_ChatRooms_AdminUserId",
                schema: "app",
                table: "ChatRooms");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_AdminUserId",
                schema: "app",
                table: "ChatRooms",
                column: "AdminUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRooms_AspNetUsers_AdminUserId",
                schema: "app",
                table: "ChatRooms",
                column: "AdminUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
