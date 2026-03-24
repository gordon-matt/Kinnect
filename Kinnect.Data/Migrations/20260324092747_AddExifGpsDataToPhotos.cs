using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExifGpsDataToPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                schema: "app",
                table: "Photos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                schema: "app",
                table: "Photos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasDivorce",
                schema: "app",
                table: "PersonSpouses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasEngagement",
                schema: "app",
                table: "PersonSpouses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasMarriage",
                schema: "app",
                table: "PersonSpouses",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                schema: "app",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "Longitude",
                schema: "app",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "HasDivorce",
                schema: "app",
                table: "PersonSpouses");

            migrationBuilder.DropColumn(
                name: "HasEngagement",
                schema: "app",
                table: "PersonSpouses");

            migrationBuilder.DropColumn(
                name: "HasMarriage",
                schema: "app",
                table: "PersonSpouses");
        }
    }
}
