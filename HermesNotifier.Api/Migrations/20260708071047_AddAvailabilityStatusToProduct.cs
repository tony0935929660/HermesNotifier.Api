using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HermesNotifier.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAvailabilityStatusToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvailabilityStatus",
                table: "Products",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "InStock");

            // 將既有資料依舊欄位 IsAvailable 回填成三態，避免全部被預設成 InStock。
            migrationBuilder.Sql(@"
UPDATE [Products]
SET [AvailabilityStatus] = CASE
    WHEN [IsAvailable] = 1 THEN 'InStock'
    ELSE 'OutOfStock'
END;");

            migrationBuilder.CreateIndex(
                name: "IX_Products_AvailabilityStatus",
                table: "Products",
                column: "AvailabilityStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_AvailabilityStatus",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AvailabilityStatus",
                table: "Products");
        }
    }
}
