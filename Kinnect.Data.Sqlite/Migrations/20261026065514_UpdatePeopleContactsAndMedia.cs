using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Sqlite.Migrations;

/// <inheritdoc />
public partial class UpdatePeopleContactsAndMedia : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "People"
            SET "Note" = substr(
                CASE
                    WHEN trim(coalesce("Bio", '')) <> '' AND trim(coalesce("Note", '')) = '' THEN "Bio"
                    WHEN trim(coalesce("Bio", '')) <> '' AND trim(coalesce("Note", '')) <> '' THEN
                        'Bio' || char(10) || "Bio" || char(10) || char(10) || 'Notes' || char(10) || "Note"
                    ELSE "Note"
                END,
                1, 4000)
            WHERE "Bio" IS NOT NULL AND trim("Bio") <> '';
            """);

        migrationBuilder.Sql("""
            UPDATE "People"
            SET "GivenNames" = trim(replace(replace(replace("GivenNames", ', ', ' '), ',', ' '), '  ', ' '))
            WHERE "GivenNames" LIKE '%,%';
            """);

        // SQLite cannot change nullability or drop a column in place, and PRAGMA foreign_keys
        // cannot run inside a transaction. Deferring the checks keeps the rebuild in this
        // migration's transaction.
        migrationBuilder.Sql("PRAGMA defer_foreign_keys = ON;");

        migrationBuilder.AddColumn<string>(
            name: "GedcomFileReference",
            schema: "app",
            table: "Videos",
            type: "TEXT",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "NamePrefix",
            schema: "app",
            table: "People",
            type: "TEXT",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "NameSuffix",
            schema: "app",
            table: "People",
            type: "TEXT",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Nickname",
            schema: "app",
            table: "People",
            type: "TEXT",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GedcomFileReference",
            schema: "app",
            table: "Documents",
            type: "TEXT",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "BackupSettings",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                KeepCount = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_BackupSettings", x => x.Id));

        migrationBuilder.Sql("""
            CREATE TABLE "ef_temp_People" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_People" PRIMARY KEY AUTOINCREMENT,
                "CreatedAtUtc" TEXT NOT NULL,
                "Education" TEXT NULL,
                "FamilyName" TEXT NOT NULL,
                "FatherId" INTEGER NULL,
                "GedcomId" TEXT NULL,
                "GivenNames" TEXT NOT NULL,
                "IsDeceased" INTEGER NOT NULL,
                "IsMale" INTEGER NULL,
                "Latitude" REAL NULL,
                "Longitude" REAL NULL,
                "MotherId" INTEGER NULL,
                "NamePrefix" TEXT NULL,
                "NameSuffix" TEXT NULL,
                "Nickname" TEXT NULL,
                "Note" TEXT NULL,
                "Occupation" TEXT NULL,
                "ProfileImagePath" TEXT NULL,
                "Religion" TEXT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "UserId" TEXT NULL,
                CONSTRAINT "FK_People_People_FatherId" FOREIGN KEY ("FatherId") REFERENCES "People" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_People_People_MotherId" FOREIGN KEY ("MotherId") REFERENCES "People" ("Id") ON DELETE SET NULL
            );
            """);

        migrationBuilder.Sql("""
            INSERT INTO "ef_temp_People" ("Id", "CreatedAtUtc", "Education", "FamilyName", "FatherId", "GedcomId", "GivenNames", "IsDeceased", "IsMale", "Latitude", "Longitude", "MotherId", "NamePrefix", "NameSuffix", "Nickname", "Note", "Occupation", "ProfileImagePath", "Religion", "UpdatedAtUtc", "UserId")
            SELECT "Id", "CreatedAtUtc", "Education", "FamilyName", "FatherId", "GedcomId", "GivenNames", "IsDeceased", "IsMale", "Latitude", "Longitude", "MotherId", "NamePrefix", "NameSuffix", "Nickname", "Note", "Occupation", "ProfileImagePath", "Religion", "UpdatedAtUtc", "UserId"
            FROM "People";
            """);

        migrationBuilder.Sql("DROP TABLE \"People\";");

        migrationBuilder.Sql("ALTER TABLE \"ef_temp_People\" RENAME TO \"People\";");

        migrationBuilder.Sql("CREATE INDEX \"IX_People_FatherId\" ON \"People\" (\"FatherId\");");

        migrationBuilder.Sql("CREATE INDEX \"IX_People_MotherId\" ON \"People\" (\"MotherId\");");

        migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_People_UserId\" ON \"People\" (\"UserId\") WHERE \"UserId\" IS NOT NULL;");

        migrationBuilder.DropTable(
            name: "Contacts",
            schema: "app");

        migrationBuilder.CreateTable(
            name: "Contacts",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
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

        migrationBuilder.CreateIndex(
            name: "IX_Contacts_PersonId_SortOrder",
            schema: "app",
            table: "Contacts",
            columns: new[] { "PersonId", "SortOrder" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("PRAGMA defer_foreign_keys = ON;");

        migrationBuilder.DropTable(
            name: "BackupSettings",
            schema: "app");

        migrationBuilder.Sql("ALTER TABLE \"Videos\" DROP COLUMN \"GedcomFileReference\";");

        migrationBuilder.Sql("ALTER TABLE \"Documents\" DROP COLUMN \"GedcomFileReference\";");

        migrationBuilder.DropTable(
            name: "Contacts",
            schema: "app");

        migrationBuilder.Sql("""
            CREATE TABLE "ef_temp_People" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_People" PRIMARY KEY AUTOINCREMENT,
                "Bio" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "Education" TEXT NULL,
                "FamilyName" TEXT NOT NULL,
                "FatherId" INTEGER NULL,
                "GedcomId" TEXT NULL,
                "GivenNames" TEXT NOT NULL,
                "IsDeceased" INTEGER NOT NULL,
                "IsMale" INTEGER NOT NULL,
                "Latitude" REAL NULL,
                "Longitude" REAL NULL,
                "MotherId" INTEGER NULL,
                "Note" TEXT NULL,
                "Occupation" TEXT NULL,
                "ProfileImagePath" TEXT NULL,
                "Religion" TEXT NULL,
                "UpdatedAtUtc" TEXT NOT NULL,
                "UserId" TEXT NULL,
                CONSTRAINT "FK_People_People_FatherId" FOREIGN KEY ("FatherId") REFERENCES "People" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_People_People_MotherId" FOREIGN KEY ("MotherId") REFERENCES "People" ("Id") ON DELETE SET NULL
            );
            """);

        migrationBuilder.Sql("""
            INSERT INTO "ef_temp_People" ("Id", "Bio", "CreatedAtUtc", "Education", "FamilyName", "FatherId", "GedcomId", "GivenNames", "IsDeceased", "IsMale", "Latitude", "Longitude", "MotherId", "Note", "Occupation", "ProfileImagePath", "Religion", "UpdatedAtUtc", "UserId")
            SELECT "Id", NULL, "CreatedAtUtc", "Education", "FamilyName", "FatherId", "GedcomId", "GivenNames", "IsDeceased", coalesce("IsMale", 0), "Latitude", "Longitude", "MotherId", "Note", "Occupation", "ProfileImagePath", "Religion", "UpdatedAtUtc", "UserId"
            FROM "People";
            """);

        migrationBuilder.Sql("DROP TABLE \"People\";");

        migrationBuilder.Sql("ALTER TABLE \"ef_temp_People\" RENAME TO \"People\";");

        migrationBuilder.Sql("CREATE INDEX \"IX_People_FatherId\" ON \"People\" (\"FatherId\");");

        migrationBuilder.Sql("CREATE INDEX \"IX_People_MotherId\" ON \"People\" (\"MotherId\");");

        migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_People_UserId\" ON \"People\" (\"UserId\") WHERE \"UserId\" IS NOT NULL;");

        migrationBuilder.CreateTable(
            name: "Contacts",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
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

        migrationBuilder.CreateIndex(
            name: "IX_Contacts_PersonId_SortOrder",
            schema: "app",
            table: "Contacts",
            columns: new[] { "PersonId", "SortOrder" });
    }
}
