using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HermesNotifier.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIsAdminToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsAdmin",
                table: "Users",
                column: "IsAdmin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_IsAdmin",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Users");
        }
    }
}
