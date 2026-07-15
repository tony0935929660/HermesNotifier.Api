# HermesNotifier.Api

基於 ASP.NET Core 8.0 與 LINE Messaging API 整合的後端服務專案。

## 🚀 技術棧

- **.NET 8.0** - 最新的 .NET 框架
- **ASP.NET Core Web API** - RESTful API 開發
- **Entity Framework Core 8.0** - ORM 資料庫存取
- **SQL Server** - 關聯式資料庫
- **LINE Messaging API** - LINE Bot 訊息推送
- **Swagger/OpenAPI** - API 文件與測試

## 📦 主要套件

- `Microsoft.EntityFrameworkCore.SqlServer` (8.0.0)
- `Microsoft.EntityFrameworkCore.Tools` (8.0.0)
- `Swashbuckle.AspNetCore` (6.6.2)
- `System.IdentityModel.Tokens.Jwt` (8.17.0)

## 🏗️ 專案結構

```
HermesNotifier.Api/
├── Controllers/
│   ├── LineController.cs          # LINE Bot 相關 API
│   └── ProductController.cs        # 商品同步與管理 API
├── Data/
│   └── ApplicationDbContext.cs     # EF Core 資料庫上下文
├── DTOs/
│   ├── Requests/                   # 請求 DTO
│   └── Responses/                  # 回應 DTO
├── Models/
│   ├── User.cs                     # 用戶資料模型
│   ├── Product.cs                  # 商品資料模型
│   └── ProductLog.cs               # 商品變更記錄模型
├── Migrations/                     # EF Core 資料庫遷移
├── Program.cs                      # 應用程式進入點
└── appsettings.json                # 設定檔
```

## ⚙️ 環境設定

### 前置需求

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (LocalDB 或完整版本)
- LINE Developers 帳號（用於取得 Channel 資訊）

### 設定步驟

1. **安裝相依套件**
   ```bash
   dotnet restore
   ```

2. **設定 appsettings.json**

   複製 `appsettings.template.json` 並重新命名為 `appsettings.json`，然後填入您的設定：

   ```json
   {
	 "ConnectionStrings": {
	   "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=HermesNotifierDb;Trusted_Connection=true;MultipleActiveResultSets=true"
	 },
	 "Line": {
	   "ChannelId": "YOUR_LINE_CHANNEL_ID",
	   "ChannelSecret": "YOUR_LINE_CHANNEL_SECRET",
	   "ChannelAccessToken": "YOUR_LINE_CHANNEL_ACCESS_TOKEN",
	   "CallbackUrl": "https://yourdomain.com/api/line/callback",
	   "LiffId": "YOUR_LIFF_ID"
	 }
   }
   ```

3. **建立資料庫**
   ```bash
   dotnet ef database update
   ```

4. **執行專案**
   ```bash
   dotnet run
   ```

   預設會在 `https://localhost:7xxx` 和 `http://localhost:5xxx` 啟動

## 📚 API 端點

### LINE 相關

- `GET /api/line/bind` - 帳號綁定頁面
- `POST /api/line/callback` - Webhook 回調端點
- `GET /api/line/token` - 取得 OAuth Token
- `GET /api/line/profile` - 取得用戶資料

### 商品管理

- `POST /api/products/sync` - 同步商品資料
- `GET /api/admin/products` - 管理員查詢商品（可依分類、關鍵字、狀態篩選）
- `GET /api/admin/logs` - 管理員查詢 LOG（可依關鍵字篩選，回傳每商品最新狀態）

## 🗄️ 資料模型

### User
- LINE ID
- 用戶名稱
- 訂閱到期時間
- 建立/更新/最後登入時間

### Product
- 商品 ID
- 標題
- 價格
- 圖片 URL
- 商品 URL
- 顏色
- 可用狀態
- 建立/更新時間

### ProductLog
- 商品狀態變更記錄

## 🔐 LINE Bot 設定

### Messaging API

1. 前往 [LINE Developers Console](https://developers.line.biz/console/)
2. 建立 Provider 和 Messaging API Channel
3. 取得 Channel ID、Channel Secret、Channel Access Token
4. 設定 Webhook URL：`https://your-domain.com/api/line/callback`
5. 啟用 "Use webhook"

### LIFF 設定

1. 建立 LIFF App
2. 設定 Endpoint URL：`https://your-domain.com/api/line/bind`
3. 選擇 Scope：`profile` 和 `openid`
4. 取得 LIFF ID

## 📝 開發指南

### 資料庫遷移

新增遷移：
```bash
dotnet ef migrations add MigrationName
```

更新資料庫：
```bash
dotnet ef database update
```

## 🚀 部署

發布應用程式：
```bash
dotnet publish -c Release -o ./publish
```

## 👤 作者

GitHub: [@tony0935929660](https://github.com/tony0935929660)
