using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class Added_Photo_LatLongAcquiredFromExif : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LatLongAcquiredFromExif",
                schema: "app",
                table: "Photos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatLongAcquiredFromExif",
                schema: "app",
                table: "Photos");
        }
    }
}
