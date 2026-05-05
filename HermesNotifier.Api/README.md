# HermesNotifier.Api Configuration

## Setup Instructions

1. Copy `appsettings.template.json` to `appsettings.json`
2. Copy `appsettings.template.json` to `appsettings.Development.json` (optional for development)
3. Update the configuration values with your actual LINE credentials:

### LINE Configuration

- **ChannelId**: Your LINE Channel ID from LINE Developers Console
- **ChannelSecret**: Your LINE Channel Secret from LINE Developers Console
- **ChannelAccessToken**: Your LINE Channel Access Token from LINE Developers Console (Messaging API tab)
- **CallbackUrl**: Your OAuth callback URL (e.g., `https://yourdomain.com/api/line/callback`)
- **LiffId**: Your LIFF App ID from LINE Developers Console (LIFF tab, format: `1234567890-abcdefgh`)

### Example

```json
{
  "Line": {
	"ChannelId": "1234567890",
	"ChannelSecret": "abcdef1234567890abcdef1234567890",
	"ChannelAccessToken": "your_channel_access_token_here",
	"CallbackUrl": "https://localhost:7001/api/line/callback",
	"LiffId": "1234567890-abcdefgh"
  }
}
```

### Setting up LIFF

1. Go to [LINE Developers Console](https://developers.line.biz/console/)
2. Select your channel
3. Go to the "LIFF" tab
4. Click "Add" to create a new LIFF app
5. Configure:
   - **LIFF app name**: HermesNotifier Callback (or any name you prefer)
   - **Size**: Full
   - **Endpoint URL**: `https://yourdomain.com/api/line/callback`
   - **Scope**: `profile` and `openid`
   - **Bot link feature**: Optional
6. Copy the generated LIFF ID (format: `1234567890-abcdefgh`)
7. Add it to your `appsettings.json` as `Line:LiffId`

**Note**: Never commit `appsettings.json` or `appsettings.Development.json` to version control as they contain sensitive credentials.
