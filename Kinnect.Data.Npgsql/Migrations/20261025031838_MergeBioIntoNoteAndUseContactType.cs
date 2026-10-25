using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kinnect.Data.Npgsql.Migrations
{
    /// <inheritdoc />
    public partial class MergeBioIntoNoteAndUseContactType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE app."People"
                SET "Note" = left(
                    CASE
                        WHEN trim(coalesce("Bio", '')) <> '' AND trim(coalesce("Note", '')) = '' THEN "Bio"
                        WHEN trim(coalesce("Bio", '')) <> '' AND trim(coalesce("Note", '')) <> '' THEN
                            'Bio' || chr(10) || "Bio" || chr(10) || chr(10) || 'Notes' || chr(10) || "Note"
                        ELSE "Note"
                    END,
                    4000)
                WHERE "Bio" IS NOT NULL AND trim("Bio") <> '';
                """);

            migrationBuilder.Sql("""
                UPDATE app."People"
                SET "GivenNames" = trim(replace(replace(replace("GivenNames", ', ', ' '), ',', ' '), '  ', ' '))
                WHERE "GivenNames" LIKE '%,%';
                """);

            migrationBuilder.DropTable(
                name: "Contacts",
                schema: "app");

            migrationBuilder.CreateTable(
                name: "Contacts",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contacts_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "app",
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_PersonId_SortOrder",
                schema: "app",
                table: "Contacts",
                columns: new[] { "PersonId", "SortOrder" });

            migrationBuilder.DropColumn(
                name: "Bio",
                schema: "app",
                table: "People");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contacts",
                schema: "app");

            migrationBuilder.CreateTable(
                name: "Contacts",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contacts_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "app",
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_PersonId_SortOrder",
                schema: "app",
                table: "Contacts",
                columns: new[] { "PersonId", "SortOrder" });

            migrationBuilder.AddColumn<string>(
                name: "Bio",
                schema: "app",
                table: "People",
                type: "text",
                nullable: true);
        }
    }
}
