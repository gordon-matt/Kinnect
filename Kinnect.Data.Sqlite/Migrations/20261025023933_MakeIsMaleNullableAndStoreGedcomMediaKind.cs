using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Sqlite.Migrations
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
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsMale",
                schema: "app",
                table: "People",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "GedcomFileReference",
                schema: "app",
                table: "Documents",
                type: "TEXT",
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
                type: "INTEGER",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
