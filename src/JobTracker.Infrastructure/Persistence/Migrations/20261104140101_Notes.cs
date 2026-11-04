using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Notes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Highlights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Section = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 20000, nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Suffix = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Start = table.Column<int>(type: "INTEGER", nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LegacyId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Highlights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ideas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    Project = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Question = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DoneOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    LegacyId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ideas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Highlights_UserId",
                table: "Highlights",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Highlights_UserId_LegacyId",
                table: "Highlights",
                columns: new[] { "UserId", "LegacyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ideas_UserId",
                table: "Ideas",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Ideas_UserId_LegacyId",
                table: "Ideas",
                columns: new[] { "UserId", "LegacyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Highlights");

            migrationBuilder.DropTable(
                name: "Ideas");
        }
    }
}
