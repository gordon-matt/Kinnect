using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kinnect.Data.Migrations;

/// <inheritdoc />
public partial class AddFolderSupportForMedia : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "FolderId",
            schema: "app",
            table: "Videos",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AnnotationsJson",
            schema: "app",
            table: "Photos",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "FolderId",
            schema: "app",
            table: "Photos",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "MediaFolders",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "text", nullable: true),
                CreatedByPersonId = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MediaFolders", x => x.Id);
                table.ForeignKey(
                    name: "FK_MediaFolders_People_CreatedByPersonId",
                    column: x => x.CreatedByPersonId,
                    principalSchema: "app",
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Videos_FolderId",
            schema: "app",
            table: "Videos",
            column: "FolderId");

        migrationBuilder.CreateIndex(
            name: "IX_Photos_FolderId",
            schema: "app",
            table: "Photos",
            column: "FolderId");

        migrationBuilder.CreateIndex(
            name: "IX_MediaFolders_CreatedByPersonId",
            schema: "app",
            table: "MediaFolders",
            column: "CreatedByPersonId");

        migrationBuilder.AddForeignKey(
            name: "FK_Photos_MediaFolders_FolderId",
            schema: "app",
            table: "Photos",
            column: "FolderId",
            principalSchema: "app",
            principalTable: "MediaFolders",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_Videos_MediaFolders_FolderId",
            schema: "app",
            table: "Videos",
            column: "FolderId",
            principalSchema: "app",
            principalTable: "MediaFolders",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Photos_MediaFolders_FolderId",
            schema: "app",
            table: "Photos");

        migrationBuilder.DropForeignKey(
            name: "FK_Videos_MediaFolders_FolderId",
            schema: "app",
            table: "Videos");

        migrationBuilder.DropTable(
            name: "MediaFolders",
            schema: "app");

        migrationBuilder.DropIndex(
            name: "IX_Videos_FolderId",
            schema: "app",
            table: "Videos");

        migrationBuilder.DropIndex(
            name: "IX_Photos_FolderId",
            schema: "app",
            table: "Photos");

        migrationBuilder.DropColumn(
            name: "FolderId",
            schema: "app",
            table: "Videos");

        migrationBuilder.DropColumn(
            name: "AnnotationsJson",
            schema: "app",
            table: "Photos");

        migrationBuilder.DropColumn(
            name: "FolderId",
            schema: "app",
            table: "Photos");
    }
}