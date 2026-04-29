# HermesNotifier.Api Configuration

## Setup Instructions

1. Copy `appsettings.template.json` to `appsettings.json`
2. Copy `appsettings.template.json` to `appsettings.Development.json` (optional for development)
3. Update the configuration values with your actual LINE credentials:

### LINE Configuration

- **ChannelId**: Your LINE Channel ID from LINE Developers Console
- **ChannelSecret**: Your LINE Channel Secret from LINE Developers Console
- **CallbackUrl**: Your OAuth callback URL (e.g., `https://yourdomain.com/api/line/callback`)

### Example

```json
{
  "Line": {
	"ChannelId": "1234567890",
	"ChannelSecret": "abcdef1234567890abcdef1234567890",
	"CallbackUrl": "https://localhost:7001/api/line/callback"
  }
}
```

**Note**: Never commit `appsettings.json` or `appsettings.Development.json` to version control as they contain sensitive credentials.
