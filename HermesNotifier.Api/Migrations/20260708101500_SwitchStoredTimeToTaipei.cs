using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HermesNotifier.Api.Migrations;

[Migration("20260708101500_SwitchStoredTimeToTaipei")]
public class SwitchStoredTimeToTaipei : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 一次性把既有資料由 UTC 基準轉為台灣時間儲存（+8 小時）。
        migrationBuilder.Sql(@"
UPDATE [Users]
SET
    [CreatedAt] = DATEADD(HOUR, 8, [CreatedAt]),
    [UpdatedAt] = CASE WHEN [UpdatedAt] IS NULL THEN NULL ELSE DATEADD(HOUR, 8, [UpdatedAt]) END,
    [LastLoginAt] = CASE WHEN [LastLoginAt] IS NULL THEN NULL ELSE DATEADD(HOUR, 8, [LastLoginAt]) END,
    [SubscribedUntil] = CASE WHEN [SubscribedUntil] IS NULL THEN NULL ELSE DATEADD(HOUR, 8, [SubscribedUntil]) END;

UPDATE [Products]
SET
    [CreatedAt] = DATEADD(HOUR, 8, [CreatedAt]),
    [UpdatedAt] = CASE WHEN [UpdatedAt] IS NULL THEN NULL ELSE DATEADD(HOUR, 8, [UpdatedAt]) END,
    [CacheExpiresAt] = CASE WHEN [CacheExpiresAt] IS NULL THEN NULL ELSE DATEADD(HOUR, 8, [CacheExpiresAt]) END;

UPDATE [ProductLogs]
SET
    [LoggedAt] = DATEADD(HOUR, 8, [LoggedAt]);
");

        migrationBuilder.AlterColumn<DateTime>(
            name: "CreatedAt",
            table: "Users",
            type: "datetime2",
            nullable: false,
            defaultValueSql: "DATEADD(HOUR, 8, GETUTCDATE())",
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldDefaultValueSql: "GETUTCDATE()");

        migrationBuilder.AlterColumn<DateTime?>(
            name: "SubscribedUntil",
            table: "Users",
            type: "datetime2",
            nullable: true,
            defaultValueSql: "DATEADD(YEAR, 1, DATEADD(HOUR, 8, GETUTCDATE()))",
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldNullable: true,
            oldDefaultValueSql: "DATEADD(YEAR, 1, GETUTCDATE())");

        migrationBuilder.AlterColumn<DateTime>(
            name: "CreatedAt",
            table: "Products",
            type: "datetime2",
            nullable: false,
            defaultValueSql: "DATEADD(HOUR, 8, GETUTCDATE())",
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldDefaultValueSql: "GETUTCDATE()");

        migrationBuilder.AlterColumn<DateTime>(
            name: "LoggedAt",
            table: "ProductLogs",
            type: "datetime2",
            nullable: false,
            defaultValueSql: "DATEADD(HOUR, 8, GETUTCDATE())",
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldDefaultValueSql: "GETUTCDATE()");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<DateTime>(
            name: "CreatedAt",
            table: "Users",
            type: "datetime2",
            nullable: false,
            defaultValueSql: "GETUTCDATE()",
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldDefaultValueSql: "DATEADD(HOUR, 8, GETUTCDATE())");

        migrationBuilder.AlterColumn<DateTime?>(
            name: "SubscribedUntil",
            table: "Users",
            type: "datetime2",
            nullable: true,
            defaultValueSql: "DATEADD(YEAR, 1, GETUTCDATE())",
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldNullable: true,
            oldDefaultValueSql: "DATEADD(YEAR, 1, DATEADD(HOUR, 8, GETUTCDATE()))");

        migrationBuilder.AlterColumn<DateTime>(
            name: "CreatedAt",
            table: "Products",
            type: "datetime2",
            nullable: false,
            defaultValueSql: "GETUTCDATE()",
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldDefaultValueSql: "DATEADD(HOUR, 8, GETUTCDATE())");

        migrationBuilder.AlterColumn<DateTime>(
            name: "LoggedAt",
            table: "ProductLogs",
            type: "datetime2",
            nullable: false,
            defaultValueSql: "GETUTCDATE()",
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldDefaultValueSql: "DATEADD(HOUR, 8, GETUTCDATE())");

        // 回滾時將已轉換資料減回 8 小時。
        migrationBuilder.Sql(@"
UPDATE [Users]
SET
    [CreatedAt] = DATEADD(HOUR, -8, [CreatedAt]),
    [UpdatedAt] = CASE WHEN [UpdatedAt] IS NULL THEN NULL ELSE DATEADD(HOUR, -8, [UpdatedAt]) END,
    [LastLoginAt] = CASE WHEN [LastLoginAt] IS NULL THEN NULL ELSE DATEADD(HOUR, -8, [LastLoginAt]) END,
    [SubscribedUntil] = CASE WHEN [SubscribedUntil] IS NULL THEN NULL ELSE DATEADD(HOUR, -8, [SubscribedUntil]) END;

UPDATE [Products]
SET
    [CreatedAt] = DATEADD(HOUR, -8, [CreatedAt]),
    [UpdatedAt] = CASE WHEN [UpdatedAt] IS NULL THEN NULL ELSE DATEADD(HOUR, -8, [UpdatedAt]) END,
    [CacheExpiresAt] = CASE WHEN [CacheExpiresAt] IS NULL THEN NULL ELSE DATEADD(HOUR, -8, [CacheExpiresAt]) END;

UPDATE [ProductLogs]
SET
    [LoggedAt] = DATEADD(HOUR, -8, [LoggedAt]);
");
    }
}
