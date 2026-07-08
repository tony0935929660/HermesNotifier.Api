BEGIN TRANSACTION;
GO

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
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'CreatedAt');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [Users] ADD DEFAULT (DATEADD(HOUR, 8, GETUTCDATE())) FOR [CreatedAt];
GO

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'SubscribedUntil');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [Users] ALTER COLUMN [SubscribedUntil] datetime2 NULL;
ALTER TABLE [Users] ADD DEFAULT (DATEADD(YEAR, 1, DATEADD(HOUR, 8, GETUTCDATE()))) FOR [SubscribedUntil];
GO

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'CreatedAt');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [Products] ADD DEFAULT (DATEADD(HOUR, 8, GETUTCDATE())) FOR [CreatedAt];
GO

DECLARE @var3 sysname;
SELECT @var3 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductLogs]') AND [c].[name] = N'LoggedAt');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [ProductLogs] DROP CONSTRAINT [' + @var3 + '];');
ALTER TABLE [ProductLogs] ADD DEFAULT (DATEADD(HOUR, 8, GETUTCDATE())) FOR [LoggedAt];
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260708101500_SwitchStoredTimeToTaipei', N'8.0.0');
GO

COMMIT;
GO

