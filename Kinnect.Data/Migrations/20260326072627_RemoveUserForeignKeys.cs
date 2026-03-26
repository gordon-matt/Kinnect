using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_AspNetUsers_FromUserId",
                schema: "app",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_AspNetUsers_ToUserId",
                schema: "app",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_People_AspNetUsers_UserId",
                schema: "app",
                table: "People");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_FromUserId",
                schema: "app",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ToUserId",
                schema: "app",
                table: "ChatMessages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_FromUserId",
                schema: "app",
                table: "ChatMessages",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ToUserId",
                schema: "app",
                table: "ChatMessages",
                column: "ToUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_AspNetUsers_FromUserId",
                schema: "app",
                table: "ChatMessages",
                column: "FromUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_AspNetUsers_ToUserId",
                schema: "app",
                table: "ChatMessages",
                column: "ToUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_People_AspNetUsers_UserId",
                schema: "app",
                table: "People",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
