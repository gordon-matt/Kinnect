using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Npgsql.Migrations
{
    /// <inheritdoc />
    public partial class MakeIsMaleNullableAndStoreGedcomMediaKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GedcomFileReference",
                schema: "app",
                table: "Videos",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsMale",
                schema: "app",
                table: "People",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<string>(
                name: "GedcomFileReference",
                schema: "app",
                table: "Documents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GedcomFileReference",
                schema: "app",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "GedcomFileReference",
                schema: "app",
                table: "Documents");

            migrationBuilder.AlterColumn<bool>(
                name: "IsMale",
                schema: "app",
                table: "People",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);
        }
    }
}
