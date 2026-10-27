using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Sql.Migrations;

/// <inheritdoc />
public partial class AddContactsAndSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Contacts",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                PersonId = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                SortOrder = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Contacts", x => x.Id);
                table.ForeignKey(
                    name: "FK_Contacts_People_PersonId",
                    column: x => x.PersonId,
                    principalSchema: "app",
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "DocumentSettings",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                AllowedExtensions = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                MaxFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                AutoShrinkDocuments = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DocumentSettings", x => x.Id));

        migrationBuilder.CreateTable(
            name: "EmailSettings",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Host = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                Port = table.Column<int>(type: "int", nullable: false),
                EnableSsl = table.Column<bool>(type: "bit", nullable: false),
                Username = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Password = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                FromAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                FromName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_EmailSettings", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ImageSettings",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                AutoShrinkImages = table.Column<bool>(type: "bit", nullable: false),
                MaxWidth = table.Column<int>(type: "int", nullable: false),
                MaxHeight = table.Column<int>(type: "int", nullable: false),
                Quality = table.Column<int>(type: "int", nullable: false),
                ThumbnailWidth = table.Column<int>(type: "int", nullable: false),
                ThumbnailHeight = table.Column<int>(type: "int", nullable: false),
                ThumbnailQuality = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ImageSettings", x => x.Id));

        migrationBuilder.CreateTable(
            name: "TreeSettings",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                MaxGenerations = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_TreeSettings", x => x.Id));

        migrationBuilder.CreateTable(
            name: "VideoSettings",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ToolsPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                AutoShrinkVideos = table.Column<bool>(type: "bit", nullable: false),
                OutputVideoSize = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                Crf = table.Column<int>(type: "int", nullable: false),
                AudioBitrate = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_VideoSettings", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Contacts_PersonId_SortOrder",
            schema: "app",
            table: "Contacts",
            columns: new[] { "PersonId", "SortOrder" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Contacts",
            schema: "app");

        migrationBuilder.DropTable(
            name: "DocumentSettings",
            schema: "app");

        migrationBuilder.DropTable(
            name: "EmailSettings",
            schema: "app");

        migrationBuilder.DropTable(
            name: "ImageSettings",
            schema: "app");

        migrationBuilder.DropTable(
            name: "TreeSettings",
            schema: "app");

        migrationBuilder.DropTable(
            name: "VideoSettings",
            schema: "app");
    }
}
