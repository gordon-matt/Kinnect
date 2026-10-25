using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddGedcomFilesAndPhotoReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GedcomFileReference",
                schema: "app",
                table: "Photos",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GedcomFiles",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    StoragePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GedcomFiles", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GedcomFiles",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "GedcomFileReference",
                schema: "app",
                table: "Photos");
        }
    }
}
