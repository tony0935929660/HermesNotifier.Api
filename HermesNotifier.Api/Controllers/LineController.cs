using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using HermesNotifier.Api.DTOs.Responses.Lines;

namespace HermesNotifier.Api.Controllers
{
    [Route("api/line")]
    [ApiController]
    public class LineController : ControllerBase
    {
        private readonly IConfiguration _config;

        public LineController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet("bind")]
        public IActionResult Bind()
        {
            var channelId = _config["Line:ChannelId"]
                ?? throw new InvalidOperationException("Line:ChannelId is missing");
            var callbackUrl = _config["Line:CallbackUrl"] 
                ?? throw new InvalidOperationException("Line:CallbackUrl is missing");
            var state = Guid.NewGuid().ToString("N");

            var url =
                "https://access.line.me/oauth2/v2.1/authorize" +
                "?response_type=code" +
                $"&client_id={channelId}" +
                $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
                $"&state={state}" +
                "&scope=profile%20openid";

            return Redirect(url);
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
        {
            var channelId = _config["Line:ChannelId"]
                ?? throw new InvalidOperationException("Line:ChannelId is missing");

            var channelSecret = _config["Line:ChannelSecret"]
                ?? throw new InvalidOperationException("Line:ChannelSecret is missing");

            var callbackUrl = _config["Line:CallbackUrl"]
                ?? throw new InvalidOperationException("Line:CallbackUrl is missing");

            using var httpClient = new HttpClient();

            var form = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", callbackUrl },
                { "client_id", channelId },
                { "client_secret", channelSecret }
            };

            var response = await httpClient.PostAsync(
                "https://api.line.me/oauth2/v2.1/token",
                new FormUrlEncodedContent(form)
            );

            var responseBody = await response.Content.ReadAsStringAsync();

            var tokenResponse = JsonSerializer.Deserialize<LineTokenResponse>(
                responseBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

            var idToken = tokenResponse?.IdToken;

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(idToken);

            var lineUserId = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            return Content($"Line UserId: {lineUserId}");
        }
    }
}
