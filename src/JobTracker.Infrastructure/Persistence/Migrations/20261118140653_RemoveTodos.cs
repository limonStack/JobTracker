using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTodos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // список дел убран — записанные дела не теряются, а становятся заметками (тот же id, владелец и день)
            migrationBuilder.Sql("""
                INSERT INTO "Ideas" ("Id", "Title", "Note", "Project", "Question", "Status", "CreatedOn", "DoneOn", "LegacyId", "UserId")
                SELECT "Id", "Title", '', '', '', 'New', "PlannedOn", NULL, NULL, "UserId" FROM "Todos";
                """);

            migrationBuilder.DropTable(
                name: "Todos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Todos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DoneOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PlannedOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Todos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Todos_UserId",
                table: "Todos",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Todos_UserId_PlannedOn",
                table: "Todos",
                columns: new[] { "UserId", "PlannedOn" });
        }
    }
}
