using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class ModifyChatEntitiesSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "ChatRooms",
                schema: "chat",
                newName: "ChatRooms",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "ChatMessages",
                schema: "chat",
                newName: "ChatMessages",
                newSchema: "app");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "chat");

            migrationBuilder.RenameTable(
                name: "ChatRooms",
                schema: "app",
                newName: "ChatRooms",
                newSchema: "chat");

            migrationBuilder.RenameTable(
                name: "ChatMessages",
                schema: "app",
                newName: "ChatMessages",
                newSchema: "chat");
        }
    }
}