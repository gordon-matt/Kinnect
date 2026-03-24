using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kinnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "chat");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeceased",
                schema: "app",
                table: "People",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ChatRooms",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AdminUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatRooms_AspNetUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FromUserId = table.Column<string>(type: "text", nullable: false),
                    ToRoomId = table.Column<int>(type: "integer", nullable: true),
                    ToUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_AspNetUsers_FromUserId",
                        column: x => x.FromUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatMessages_AspNetUsers_ToUserId",
                        column: x => x.ToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatRooms_ToRoomId",
                        column: x => x.ToRoomId,
                        principalSchema: "chat",
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_FromUserId",
                schema: "chat",
                table: "ChatMessages",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ToRoomId",
                schema: "chat",
                table: "ChatMessages",
                column: "ToRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ToUserId",
                schema: "chat",
                table: "ChatMessages",
                column: "ToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_AdminUserId",
                schema: "chat",
                table: "ChatRooms",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_Name",
                schema: "chat",
                table: "ChatRooms",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "ChatRooms",
                schema: "chat");

            migrationBuilder.DropColumn(
                name: "IsDeceased",
                schema: "app",
                table: "People");
        }
    }
}
