using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kinnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonEventAndGedcomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Education",
                schema: "app",
                table: "People",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GedcomId",
                schema: "app",
                table: "People",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                schema: "app",
                table: "People",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                schema: "app",
                table: "People",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Religion",
                schema: "app",
                table: "People",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PersonEvents",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Year = table.Column<short>(type: "smallint", nullable: true),
                    Month = table.Column<byte>(type: "smallint", nullable: true),
                    Day = table.Column<byte>(type: "smallint", nullable: true),
                    Place = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonEvents_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "app",
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonEvents_PersonId",
                schema: "app",
                table: "PersonEvents",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonEvents",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "Education",
                schema: "app",
                table: "People");

            migrationBuilder.DropColumn(
                name: "GedcomId",
                schema: "app",
                table: "People");

            migrationBuilder.DropColumn(
                name: "Note",
                schema: "app",
                table: "People");

            migrationBuilder.DropColumn(
                name: "Occupation",
                schema: "app",
                table: "People");

            migrationBuilder.DropColumn(
                name: "Religion",
                schema: "app",
                table: "People");
        }
    }
}
