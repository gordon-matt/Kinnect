using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Kinnect.Data.MySql.Migrations;

/// <inheritdoc />
public partial class UpdatePeopleContactsAndMedia : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE `app`.`People`
            SET `Note` = LEFT(
                CASE
                    WHEN TRIM(IFNULL(`Bio`, '')) <> '' AND TRIM(IFNULL(`Note`, '')) = '' THEN `Bio`
                    WHEN TRIM(IFNULL(`Bio`, '')) <> '' AND TRIM(IFNULL(`Note`, '')) <> '' THEN
                        CONCAT('Bio', CHAR(10), `Bio`, CHAR(10), CHAR(10), 'Notes', CHAR(10), `Note`)
                    ELSE `Note`
                END,
                4000)
            WHERE `Bio` IS NOT NULL AND TRIM(`Bio`) <> '';
            """);

        migrationBuilder.Sql("""
            UPDATE `app`.`People`
            SET `GivenNames` = TRIM(REPLACE(REPLACE(REPLACE(`GivenNames`, ', ', ' '), ',', ' '), '  ', ' '))
            WHERE `GivenNames` LIKE '%,%';
            """);

        migrationBuilder.DropTable(
            name: "Contacts",
            schema: "app");

        migrationBuilder.CreateTable(
            name: "Contacts",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                PersonId = table.Column<int>(type: "int", nullable: false),
                SortOrder = table.Column<int>(type: "int", nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                Value = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
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
            })
            .Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_Contacts_PersonId_SortOrder",
            schema: "app",
            table: "Contacts",
            columns: new[] { "PersonId", "SortOrder" });

        migrationBuilder.DropColumn(
            name: "Bio",
            schema: "app",
            table: "People");

        migrationBuilder.AddColumn<string>(
            name: "GedcomFileReference",
            schema: "app",
            table: "Videos",
            type: "varchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AlterColumn<bool>(
            name: "IsMale",
            schema: "app",
            table: "People",
            type: "tinyint(1)",
            nullable: true,
            oldClrType: typeof(bool),
            oldType: "tinyint(1)");

        migrationBuilder.AddColumn<string>(
            name: "NamePrefix",
            schema: "app",
            table: "People",
            type: "varchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "NameSuffix",
            schema: "app",
            table: "People",
            type: "varchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Nickname",
            schema: "app",
            table: "People",
            type: "varchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GedcomFileReference",
            schema: "app",
            table: "Documents",
            type: "varchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "BackupSettings",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                KeepCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_BackupSettings", x => x.Id))
            .Annotation("MySQL:Charset", "utf8mb4");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "BackupSettings",
            schema: "app");

        migrationBuilder.DropColumn(
            name: "GedcomFileReference",
            schema: "app",
            table: "Videos");

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

        migrationBuilder.DropColumn(
            name: "GedcomFileReference",
            schema: "app",
            table: "Documents");

        migrationBuilder.AlterColumn<bool>(
            name: "IsMale",
            schema: "app",
            table: "People",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false,
            oldClrType: typeof(bool),
            oldType: "tinyint(1)",
            oldNullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Bio",
            schema: "app",
            table: "People",
            type: "longtext",
            nullable: true);

        migrationBuilder.DropTable(
            name: "Contacts",
            schema: "app");

        migrationBuilder.CreateTable(
            name: "Contacts",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                PersonId = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                Value = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
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
            })
            .Annotation("MySQL:Charset", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_Contacts_PersonId_SortOrder",
            schema: "app",
            table: "Contacts",
            columns: new[] { "PersonId", "SortOrder" });
    }
}
