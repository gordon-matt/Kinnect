using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorVitalEventsSpouseDivorce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "DivorceDay",
                schema: "app",
                table: "PersonSpouses",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "DivorceMonth",
                schema: "app",
                table: "PersonSpouses",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "DivorceYear",
                schema: "app",
                table: "PersonSpouses",
                type: "smallint",
                nullable: true);

            // Copy legacy People birth/death columns into PersonEvents when no BIRT/DEAT row exists.
            migrationBuilder.Sql("""
                INSERT INTO app."PersonEvents" ("PersonId", "EventType", "Year", "Month", "Day", "Place", "Description", "Note", "CreatedAtUtc")
                SELECT p."Id", 'BIRT', p."YearOfBirth", p."MonthOfBirth", p."DayOfBirth", NULL, NULL, NULL, timezone('utc', now())
                FROM app."People" p
                WHERE p."YearOfBirth" IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1 FROM app."PersonEvents" e
                    WHERE e."PersonId" = p."Id" AND e."EventType" = 'BIRT');
                """);

            migrationBuilder.Sql("""
                INSERT INTO app."PersonEvents" ("PersonId", "EventType", "Year", "Month", "Day", "Place", "Description", "Note", "CreatedAtUtc")
                SELECT p."Id", 'DEAT', p."YearOfDeath", p."MonthOfDeath", p."DayOfDeath", NULL, NULL, NULL, timezone('utc', now())
                FROM app."People" p
                WHERE p."YearOfDeath" IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1 FROM app."PersonEvents" e
                    WHERE e."PersonId" = p."Id" AND e."EventType" = 'DEAT');
                """);

            migrationBuilder.Sql("""DELETE FROM app."PersonEvents" WHERE "EventType" IN ('MARR', 'DIV', 'OCCU', 'EDUC', 'RELI');""");

            migrationBuilder.DropColumn(
                name: "DayOfBirth",
                schema: "app",
                table: "People");

            migrationBuilder.DropColumn(
                name: "DayOfDeath",
                schema: "app",
                table: "People");

            migrationBuilder.DropColumn(
                name: "MonthOfBirth",
                schema: "app",
                table: "People");

            migrationBuilder.DropColumn(
                name: "MonthOfDeath",
                schema: "app",
                table: "People");

            migrationBuilder.DropColumn(
                name: "YearOfBirth",
                schema: "app",
                table: "People");

            migrationBuilder.DropColumn(
                name: "YearOfDeath",
                schema: "app",
                table: "People");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DivorceDay",
                schema: "app",
                table: "PersonSpouses");

            migrationBuilder.DropColumn(
                name: "DivorceMonth",
                schema: "app",
                table: "PersonSpouses");

            migrationBuilder.DropColumn(
                name: "DivorceYear",
                schema: "app",
                table: "PersonSpouses");

            migrationBuilder.AddColumn<byte>(
                name: "DayOfBirth",
                schema: "app",
                table: "People",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "DayOfDeath",
                schema: "app",
                table: "People",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "MonthOfBirth",
                schema: "app",
                table: "People",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "MonthOfDeath",
                schema: "app",
                table: "People",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "YearOfBirth",
                schema: "app",
                table: "People",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "YearOfDeath",
                schema: "app",
                table: "People",
                type: "smallint",
                nullable: true);
        }
    }
}
