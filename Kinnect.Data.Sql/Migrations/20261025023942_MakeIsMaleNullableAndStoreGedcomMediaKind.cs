using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Sql.Migrations
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
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsMale",
                schema: "app",
                table: "People",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<string>(
                name: "GedcomFileReference",
                schema: "app",
                table: "Documents",
                type: "nvarchar(1000)",
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
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);
        }
    }
}
