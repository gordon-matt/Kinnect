using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.MySql.Migrations
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
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsMale",
                schema: "app",
                table: "People",
                type: "tinyint(1)",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AddColumn<string>(
                name: "GedcomFileReference",
                schema: "app",
                table: "Documents",
                type: "varchar(1000)",
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
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldNullable: true);
        }
    }
}
