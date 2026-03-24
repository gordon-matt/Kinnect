using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Migrations;

/// <inheritdoc />
public partial class AddEngagementGeoAndMediaEventLinks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PlaceOfBirth",
            schema: "app",
            table: "People");

        migrationBuilder.DropColumn(
            name: "PlaceOfDeath",
            schema: "app",
            table: "People");

        migrationBuilder.AddColumn<byte>(
            name: "EngagementDay",
            schema: "app",
            table: "PersonSpouses",
            type: "smallint",
            nullable: true);

        migrationBuilder.AddColumn<byte>(
            name: "EngagementMonth",
            schema: "app",
            table: "PersonSpouses",
            type: "smallint",
            nullable: true);

        migrationBuilder.AddColumn<short>(
            name: "EngagementYear",
            schema: "app",
            table: "PersonSpouses",
            type: "smallint",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "Latitude",
            schema: "app",
            table: "PersonEvents",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "Longitude",
            schema: "app",
            table: "PersonEvents",
            type: "double precision",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "PhotoEvents",
            schema: "app",
            columns: table => new
            {
                PhotoId = table.Column<int>(type: "integer", nullable: false),
                PersonEventId = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PhotoEvents", x => new { x.PhotoId, x.PersonEventId });
                table.ForeignKey(
                    name: "FK_PhotoEvents_PersonEvents_PersonEventId",
                    column: x => x.PersonEventId,
                    principalSchema: "app",
                    principalTable: "PersonEvents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PhotoEvents_Photos_PhotoId",
                    column: x => x.PhotoId,
                    principalSchema: "app",
                    principalTable: "Photos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "VideoEvents",
            schema: "app",
            columns: table => new
            {
                VideoId = table.Column<int>(type: "integer", nullable: false),
                PersonEventId = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VideoEvents", x => new { x.VideoId, x.PersonEventId });
                table.ForeignKey(
                    name: "FK_VideoEvents_PersonEvents_PersonEventId",
                    column: x => x.PersonEventId,
                    principalSchema: "app",
                    principalTable: "PersonEvents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_VideoEvents_Videos_VideoId",
                    column: x => x.VideoId,
                    principalSchema: "app",
                    principalTable: "Videos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PhotoEvents_PersonEventId",
            schema: "app",
            table: "PhotoEvents",
            column: "PersonEventId");

        migrationBuilder.CreateIndex(
            name: "IX_VideoEvents_PersonEventId",
            schema: "app",
            table: "VideoEvents",
            column: "PersonEventId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PhotoEvents",
            schema: "app");

        migrationBuilder.DropTable(
            name: "VideoEvents",
            schema: "app");

        migrationBuilder.DropColumn(
            name: "EngagementDay",
            schema: "app",
            table: "PersonSpouses");

        migrationBuilder.DropColumn(
            name: "EngagementMonth",
            schema: "app",
            table: "PersonSpouses");

        migrationBuilder.DropColumn(
            name: "EngagementYear",
            schema: "app",
            table: "PersonSpouses");

        migrationBuilder.DropColumn(
            name: "Latitude",
            schema: "app",
            table: "PersonEvents");

        migrationBuilder.DropColumn(
            name: "Longitude",
            schema: "app",
            table: "PersonEvents");

        migrationBuilder.AddColumn<string>(
            name: "PlaceOfBirth",
            schema: "app",
            table: "People",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PlaceOfDeath",
            schema: "app",
            table: "People",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);
    }
}