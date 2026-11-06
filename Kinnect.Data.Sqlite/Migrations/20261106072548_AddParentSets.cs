using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddParentSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParentSets",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChildPersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    Year = table.Column<short>(type: "INTEGER", nullable: true),
                    Month = table.Column<byte>(type: "INTEGER", nullable: true),
                    Day = table.Column<byte>(type: "INTEGER", nullable: true),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentSets_People_ChildPersonId",
                        column: x => x.ChildPersonId,
                        principalSchema: "app",
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParentSetMembers",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParentSetId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentPersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentSetMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentSetMembers_ParentSets_ParentSetId",
                        column: x => x.ParentSetId,
                        principalSchema: "app",
                        principalTable: "ParentSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParentSetMembers_People_ParentPersonId",
                        column: x => x.ParentPersonId,
                        principalSchema: "app",
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParentSetMembers_ParentPersonId",
                schema: "app",
                table: "ParentSetMembers",
                column: "ParentPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentSetMembers_ParentSetId_ParentPersonId",
                schema: "app",
                table: "ParentSetMembers",
                columns: new[] { "ParentSetId", "ParentPersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentSetMembers_ParentSetId_Role",
                schema: "app",
                table: "ParentSetMembers",
                columns: new[] { "ParentSetId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentSets_ChildPersonId",
                schema: "app",
                table: "ParentSets",
                column: "ChildPersonId");

            // Every existing father and mother becomes one primary Birth set, visible to everyone, so nothing is lost.
            // This runs before the old columns are dropped.
            migrationBuilder.Sql(
                @"INSERT INTO ""ParentSets"" (""ChildPersonId"", ""Type"", ""IsPrimary"", ""Visibility"", ""CreatedAtUtc"")
                  SELECT p.""Id"", 0, 1, 0, datetime('now')
                  FROM ""People"" p
                  WHERE (p.""FatherId"" IS NOT NULL AND p.""FatherId"" <> p.""Id"") OR (p.""MotherId"" IS NOT NULL AND p.""MotherId"" <> p.""Id"");");

            migrationBuilder.Sql(
                @"INSERT INTO ""ParentSetMembers"" (""ParentSetId"", ""ParentPersonId"", ""Role"")
                  SELECT s.""Id"", p.""FatherId"", 0
                  FROM ""ParentSets"" s INNER JOIN ""People"" p ON p.""Id"" = s.""ChildPersonId""
                  WHERE p.""FatherId"" IS NOT NULL AND p.""FatherId"" <> p.""Id"";");

            migrationBuilder.Sql(
                @"INSERT INTO ""ParentSetMembers"" (""ParentSetId"", ""ParentPersonId"", ""Role"")
                  SELECT s.""Id"", p.""MotherId"", 1
                  FROM ""ParentSets"" s INNER JOIN ""People"" p ON p.""Id"" = s.""ChildPersonId""
                  WHERE p.""MotherId"" IS NOT NULL AND p.""MotherId"" <> p.""Id"" AND (p.""FatherId"" IS NULL OR p.""FatherId"" <> p.""MotherId"");");

            migrationBuilder.DropForeignKey(
                name: "FK_People_People_FatherId",
                schema: "app",
                table: "People");

            migrationBuilder.DropForeignKey(
                name: "FK_People_People_MotherId",
                schema: "app",
                table: "People");

            migrationBuilder.DropIndex(
                name: "IX_People_FatherId",
                schema: "app",
                table: "People");

            migrationBuilder.DropIndex(
                name: "IX_People_MotherId",
                schema: "app",
                table: "People");

            migrationBuilder.DropColumn(
                name: "FatherId",
                schema: "app",
                table: "People");

            migrationBuilder.DropColumn(
                name: "MotherId",
                schema: "app",
                table: "People");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FatherId",
                schema: "app",
                table: "People",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MotherId",
                schema: "app",
                table: "People",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_People_FatherId",
                schema: "app",
                table: "People",
                column: "FatherId");

            migrationBuilder.CreateIndex(
                name: "IX_People_MotherId",
                schema: "app",
                table: "People",
                column: "MotherId");

            migrationBuilder.AddForeignKey(
                name: "FK_People_People_FatherId",
                schema: "app",
                table: "People",
                column: "FatherId",
                principalSchema: "app",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_People_People_MotherId",
                schema: "app",
                table: "People",
                column: "MotherId",
                principalSchema: "app",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Put the primary Birth parents back in the old columns before the new tables go.
            migrationBuilder.Sql(
                @"UPDATE ""People"" SET
                    ""FatherId"" = (SELECT m.""ParentPersonId"" FROM ""ParentSetMembers"" m INNER JOIN ""ParentSets"" s ON s.""Id"" = m.""ParentSetId"" WHERE s.""ChildPersonId"" = ""People"".""Id"" AND s.""Type"" = 0 AND m.""Role"" = 0 ORDER BY s.""IsPrimary"" DESC, s.""Id"" LIMIT 1),
                    ""MotherId"" = (SELECT m.""ParentPersonId"" FROM ""ParentSetMembers"" m INNER JOIN ""ParentSets"" s ON s.""Id"" = m.""ParentSetId"" WHERE s.""ChildPersonId"" = ""People"".""Id"" AND s.""Type"" = 0 AND m.""Role"" = 1 ORDER BY s.""IsPrimary"" DESC, s.""Id"" LIMIT 1);");

            migrationBuilder.DropTable(
                name: "ParentSetMembers",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ParentSets",
                schema: "app");
        }
    }
}
