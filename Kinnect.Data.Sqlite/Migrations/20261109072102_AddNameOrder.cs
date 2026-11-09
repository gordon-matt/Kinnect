using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddNameOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CapitalizeFamilyNames",
                schema: "app",
                table: "TreeSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultNameOrder",
                schema: "app",
                table: "TreeSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFamilyNameFirst",
                schema: "app",
                table: "People",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CapitalizeFamilyNames",
                schema: "app",
                table: "TreeSettings");

            migrationBuilder.DropColumn(
                name: "DefaultNameOrder",
                schema: "app",
                table: "TreeSettings");

            migrationBuilder.DropColumn(
                name: "IsFamilyNameFirst",
                schema: "app",
                table: "People");
        }
    }
}
