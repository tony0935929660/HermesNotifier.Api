-- ============================================
-- 診斷和修復 Product 重複建立問題
-- ============================================

-- 1. 檢查 __EFMigrationsHistory 表
SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId;

-- 2. 檢查所有資料表
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME;

-- 3. 檢查 Product 和 Products 的結構
SELECT 
	TABLE_NAME,
	COLUMN_NAME,
	DATA_TYPE,
	IS_NULLABLE,
	CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('Product', 'Products')
ORDER BY TABLE_NAME, ORDINAL_POSITION;

-- ============================================
-- 修復方案 1: 如果 Product 表存在且有資料
-- ============================================

-- 檢查資料數量
SELECT 'Product' AS TableName, COUNT(*) AS RowCount FROM Product;
SELECT 'Products' AS TableName, COUNT(*) AS RowCount FROM Products;

-- 如果 Product 有資料，遷移到 Products
-- 注意: 先確認 Product 表的結構是否有 IsAvailable 和 UpdatedAt
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Product')
BEGIN
	-- 檢查 Product 表結構
	IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Product' AND COLUMN_NAME = 'IsAvailable')
	BEGIN
		-- 如果 Product 表沒有 IsAvailable，遷移時加上預設值
		INSERT INTO Products (ProductId, Title, Price, ImageUrl, ProductUrl, Color, CreatedAt, IsAvailable, UpdatedAt)
		SELECT ProductId, Title, Price, ImageUrl, ProductUrl, Color, CreatedAt, 1, NULL
		FROM Product
		WHERE NOT EXISTS (
			SELECT 1 FROM Products WHERE Products.ProductId = Product.ProductId
		);
	END
	ELSE
	BEGIN
		-- 如果 Product 表有 IsAvailable，直接遷移
		INSERT INTO Products (ProductId, Title, Price, ImageUrl, ProductUrl, Color, CreatedAt, IsAvailable, UpdatedAt)
		SELECT ProductId, Title, Price, ImageUrl, ProductUrl, Color, CreatedAt, IsAvailable, UpdatedAt
		FROM Product
		WHERE NOT EXISTS (
			SELECT 1 FROM Products WHERE Products.ProductId = Product.ProductId
		);
	END

	-- 刪除舊的 Product 表
	DROP TABLE Product;
	PRINT 'Product 表已刪除';
END

-- ============================================
-- 修復方案 2: 確保 __EFMigrationsHistory 正確
-- ============================================

-- 檢查是否缺少 migration 記錄
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20260430122947_InitialCreate')
BEGIN
	INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
	VALUES ('20260430122947_InitialCreate', '8.0.0');
	PRINT '已補上 InitialCreate migration 記錄';
END

IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20260430130225_AddProductTable')
BEGIN
	INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
	VALUES ('20260430130225_AddProductTable', '8.0.0');
	PRINT '已補上 AddProductTable migration 記錄';
END

IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20260501000000_AddProductUrlColumn')
BEGIN
	INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
	VALUES ('20260501000000_AddProductUrlColumn', '8.0.0');
	PRINT '已補上 AddProductUrlColumn migration 記錄';
END

IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20260504061453_AddIsAvailableToProduct')
BEGIN
	INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
	VALUES ('20260504061453_AddIsAvailableToProduct', '8.0.0');
	PRINT '已補上 AddIsAvailableToProduct migration 記錄';
END

IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20260504071822_AddProductLogsTable')
BEGIN
	INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
	VALUES ('20260504071822_AddProductLogsTable', '8.0.0');
	PRINT '已補上 AddProductLogsTable migration 記錄';
END

-- 最終確認
SELECT '=== 最終狀態 ===' AS Status;
SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId;
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;
