using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_LegacyId",
                table: "Applications");

            // записи, созданные до появления пользователей, достаются локальному пользователю (LocalUser.Id);
            // литерал, а не ссылка на код: миграция не должна меняться, если константа когда-нибудь поменяется
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Applications",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.CreateIndex(
                name: "IX_Applications_UserId",
                table: "Applications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_UserId_LegacyId",
                table: "Applications",
                columns: new[] { "UserId", "LegacyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_UserId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_UserId_LegacyId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Applications");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_LegacyId",
                table: "Applications",
                column: "LegacyId",
                unique: true);
        }
    }
}
