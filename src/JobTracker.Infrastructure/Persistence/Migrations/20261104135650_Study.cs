using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Study : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityDays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReviewMarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewMarks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudyMarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    StudiedOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyMarks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DailyTheoryGoal = table.Column<int>(type: "INTEGER", nullable: false),
                    WeeklyWorkoutGoal = table.Column<int>(type: "INTEGER", nullable: false),
                    CalendarOpen = table.Column<bool>(type: "INTEGER", nullable: true),
                    FocusMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    TimeZone = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityDays_UserId",
                table: "ActivityDays",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityDays_UserId_Kind_Day",
                table: "ActivityDays",
                columns: new[] { "UserId", "Kind", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewMarks_UserId",
                table: "ReviewMarks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewMarks_UserId_Day_Code",
                table: "ReviewMarks",
                columns: new[] { "UserId", "Day", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyMarks_UserId",
                table: "StudyMarks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyMarks_UserId_Code",
                table: "StudyMarks",
                columns: new[] { "UserId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_UserId",
                table: "UserSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityDays");

            migrationBuilder.DropTable(
                name: "ReviewMarks");

            migrationBuilder.DropTable(
                name: "StudyMarks");

            migrationBuilder.DropTable(
                name: "UserSettings");
        }
    }
}
