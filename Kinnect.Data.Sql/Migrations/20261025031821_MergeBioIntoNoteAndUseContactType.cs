using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Sql.Migrations
{
    /// <inheritdoc />
    public partial class MergeBioIntoNoteAndUseContactType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [app].[People]
                SET [Note] = LEFT(
                    CASE
                        WHEN NULLIF(TRIM([Bio]), '') IS NOT NULL AND NULLIF(TRIM([Note]), '') IS NULL THEN [Bio]
                        WHEN NULLIF(TRIM([Bio]), '') IS NOT NULL AND NULLIF(TRIM([Note]), '') IS NOT NULL THEN
                            N'Bio' + CHAR(10) + [Bio] + CHAR(10) + CHAR(10) + N'Notes' + CHAR(10) + [Note]
                        ELSE [Note]
                    END,
                    4000)
                WHERE NULLIF(TRIM([Bio]), '') IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE [app].[People]
                SET [GivenNames] = TRIM(REPLACE(REPLACE(REPLACE([GivenNames], ', ', ' '), ',', ' '), '  ', ' '))
                WHERE [GivenNames] LIKE '%,%';
                """);

            migrationBuilder.DropTable(
                name: "Contacts",
                schema: "app");

            migrationBuilder.CreateTable(
                name: "Contacts",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
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
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
