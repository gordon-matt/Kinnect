using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Migrations;

/// <inheritdoc />
public partial class AddPhotoDateFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte>(
            name: "DayTaken",
            schema: "app",
            table: "Photos",
            type: "smallint",
            nullable: true);

        migrationBuilder.AddColumn<byte>(
            name: "MonthTaken",
            schema: "app",
            table: "Photos",
            type: "smallint",
            nullable: true);

        migrationBuilder.AddColumn<short>(
            name: "YearTaken",
            schema: "app",
            table: "Photos",
            type: "smallint",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DayTaken",
            schema: "app",
            table: "Photos");

        migrationBuilder.DropColumn(
            name: "MonthTaken",
            schema: "app",
            table: "Photos");

        migrationBuilder.DropColumn(
            name: "YearTaken",
            schema: "app",
            table: "Photos");
    }
}