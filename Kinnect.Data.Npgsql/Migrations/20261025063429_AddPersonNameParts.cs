using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Npgsql.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonNameParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NamePrefix",
                schema: "app",
                table: "People",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameSuffix",
                schema: "app",
                table: "People",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                schema: "app",
                table: "People",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NamePrefix",
                schema: "app",
                table: "People");

            migrationBuilder.DropColumn(
                name: "NameSuffix",
                schema: "app",
                table: "People");

            migrationBuilder.DropColumn(
                name: "Nickname",
                schema: "app",
                table: "People");
        }
    }
}
