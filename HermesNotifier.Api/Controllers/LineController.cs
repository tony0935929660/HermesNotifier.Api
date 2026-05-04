using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using HermesNotifier.Api.DTOs.Responses.Lines;
using HermesNotifier.Api.Data;
using HermesNotifier.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HermesNotifier.Api.Controllers
{
    [Route("api/line")]
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

            // 記錄 LINE Token API 回應（用於診斷）
            _logger.LogInformation("LINE Token API Status: {StatusCode}", response.StatusCode);
            _logger.LogInformation("LINE Token API Response: {Response}", responseBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("LINE Token API 失敗 - StatusCode: {StatusCode}, Body: {Body}", 
                    response.StatusCode, responseBody);
                return BadRequest($"LINE Token API 錯誤: {response.StatusCode} - {responseBody}");
            }

            LineTokenResponse? tokenResponse = null;
            try
            {
                tokenResponse = JsonSerializer.Deserialize<LineTokenResponse>(
                    responseBody,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }
                );
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON 反序列化失敗 - Response: {Response}", responseBody);
                return BadRequest($"無法解析 LINE Token 回應: {ex.Message}");
            }

            if (tokenResponse?.AccessToken == null)
            {
                _logger.LogError("Access Token 為空 - Response: {Response}", responseBody);
                return BadRequest("無法取得 LINE Access Token");
            }

            // 使用 Access Token 呼叫 LINE Profile API 取得真正的 User ID
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

            var profileResponse = await httpClient.GetAsync("https://api.line.me/v2/profile");

            if (!profileResponse.IsSuccessStatusCode)
            {
                var errorBody = await profileResponse.Content.ReadAsStringAsync();
                _logger.LogError("無法取得 LINE Profile: {error}", errorBody);
                return BadRequest("無法取得 LINE 使用者資訊");
            }

            var profileBody = await profileResponse.Content.ReadAsStringAsync();
            var profileData = JsonSerializer.Deserialize<LineProfileResponse>(
                profileBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

            var lineUserId = profileData?.UserId;
            var displayName = profileData?.DisplayName;

            if (string.IsNullOrEmpty(lineUserId))
            {
                _logger.LogError("無法從 LINE Profile 取得使用者 ID");
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
