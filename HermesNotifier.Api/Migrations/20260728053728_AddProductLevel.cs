using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HermesNotifier.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "Products",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "C");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Level",
                table: "Products",
                column: "Level");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Level",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Products");
        }
    }
}
