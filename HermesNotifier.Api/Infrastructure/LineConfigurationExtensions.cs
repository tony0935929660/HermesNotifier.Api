using Microsoft.Extensions.Configuration;

namespace HermesNotifier.Api.Infrastructure;

public static class LineConfigurationExtensions
{
    public static string? GetLineChannelAccessToken(this IConfiguration config)
    {
        var token = config["Line:ChannelAccessToken"];
        if (!string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        token = config["LINE_BOT_CHANNEL_ACCESS_TOKEN"];
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}