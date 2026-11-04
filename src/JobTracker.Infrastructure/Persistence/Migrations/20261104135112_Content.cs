using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Content : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    QuestionCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sections",
                columns: table => new
                {
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    IntroHtml = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Number);
                });

            migrationBuilder.CreateTable(
                name: "QuestionGroups",
                columns: table => new
                {
                    SectionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    IntroHtml = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionGroups", x => new { x.SectionNumber, x.Order });
                    table.ForeignKey(
                        name: "FK_QuestionGroups_Sections_SectionNumber",
                        column: x => x.SectionNumber,
                        principalTable: "Sections",
                        principalColumn: "Number",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    SectionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Importance = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    BodyHtml = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Code);
                    table.ForeignKey(
                        name: "FK_Questions_QuestionGroups_SectionNumber_GroupOrder",
                        columns: x => new { x.SectionNumber, x.GroupOrder },
                        principalTable: "QuestionGroups",
                        principalColumns: new[] { "SectionNumber", "Order" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_SectionNumber_GroupOrder",
                table: "Questions",
                columns: new[] { "SectionNumber", "GroupOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentVersions");

            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "QuestionGroups");

            migrationBuilder.DropTable(
                name: "Sections");
        }
    }
}
