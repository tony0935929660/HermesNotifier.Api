using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using HermesNotifier.Api.DTOs.Responses.Lines;
using HermesNotifier.Api.Data;
using HermesNotifier.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HermesNotifier.Api.Controllers
{
    [Route("line")]
    [ApiController]
    public class LineController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LineController> _logger;

        public LineController(
            IConfiguration config,
            ApplicationDbContext context,
            ILogger<LineController> logger)
        {
            _config = config;
            _context = context;
            _logger = logger;
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
            var displayName = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value;

            if (string.IsNullOrEmpty(lineUserId))
            {
                _logger.LogError("無法從 LINE Token 取得使用者 ID");
                return BadRequest("無法取得 LINE 使用者資訊");
            }

            // 檢查使用者是否已存在
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.LineId == lineUserId);

            if (existingUser == null)
            {
                // 建立新使用者
                var newUser = new User
                {
                    LineId = lineUserId,
                    Name = displayName,
                    LastLoginAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                _logger.LogInformation("新增使用者：LineId={LineId}, Name={Name}", lineUserId, displayName);

                return Content($"歡迎！已建立帳號\nLine UserId: {lineUserId}\nName: {displayName}");
            }
            else
            {
                // 更新最後登入時間
                existingUser.LastLoginAt = DateTime.UtcNow;
                existingUser.Name = displayName; // 更新名稱（以防使用者改名）
                await _context.SaveChangesAsync();

                _logger.LogInformation("使用者登入：LineId={LineId}, Name={Name}", lineUserId, displayName);

                return Content($"歡迎回來！\nLine UserId: {lineUserId}\nName: {displayName}");
            }
        }
    }
}
