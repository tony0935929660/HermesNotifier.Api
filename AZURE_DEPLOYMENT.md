# Azure Container Apps 部署指南

## 需要的環境變數

在 Azure Container Apps 中設定以下環境變數：

### 必要設定
- `ASPNETCORE_ENVIRONMENT` = `Production`
- `ASPNETCORE_URLS` = `http://+:8080`

### LINE 設定
- `Line__ChannelId` = `2008323822`
- `Line__ChannelSecret` = `你的ChannelSecret`
- `Line__CallbackUrl` = `https://你的Azure網址/api/line/callback`
- `LINE_BOT_CHANNEL_ACCESS_TOKEN` = `你的LINE Bot Token`

### 資料庫設定
- `ConnectionStrings__DefaultConnection` = `你的Azure SQL連線字串`

## Azure 資源需求

1. **Container Registry** (或使用 GitHub Container Registry)
2. **Azure SQL Database** (或 Azure Database for PostgreSQL)
3. **Azure Container Apps**

## 快速部署命令

```bash
# 設定變數
RESOURCE_GROUP="hermesnotifier-rg"
LOCATION="eastasia"
CONTAINER_APP_NAME="hermesnotifier-api"
CONTAINER_APP_ENV="hermesnotifier-env"
IMAGE="ghcr.io/tony0935929660/hermesnotifier.api:latest"

# 建立資源群組
az group create --name $RESOURCE_GROUP --location $LOCATION

# 建立 Container Apps 環境
az containerapp env create \
  --name $CONTAINER_APP_ENV \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION

# 部署 Container App
az containerapp create \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --environment $CONTAINER_APP_ENV \
  --image $IMAGE \
  --target-port 8080 \
  --ingress external \
  --env-vars \
	"ASPNETCORE_ENVIRONMENT=Production" \
	"ASPNETCORE_URLS=http://+:8080" \
	"Line__ChannelId=2008323822" \
	"Line__ChannelSecret=secretref:line-channel-secret" \
	"Line__CallbackUrl=https://你的網址/api/line/callback" \
  --secrets \
	"line-channel-secret=你的ChannelSecret" \
	"line-bot-token=你的BotToken" \
	"connection-string=你的資料庫連線字串"
```

## 部署後設定

1. 取得 Container App URL
2. 更新 LINE Developer Console 的 Callback URL
3. 更新環境變數中的 `Line__CallbackUrl`
4. 執行資料庫 Migration
