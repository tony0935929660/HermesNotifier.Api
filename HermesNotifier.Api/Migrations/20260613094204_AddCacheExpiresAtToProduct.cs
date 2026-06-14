using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HermesNotifier.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCacheExpiresAtToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CacheExpiresAt",
                table: "Products",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CacheExpiresAt",
                table: "Products",
                column: "CacheExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_CacheExpiresAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CacheExpiresAt",
                table: "Products");
        }
    }
}
