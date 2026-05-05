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

                return GenerateSuccessHtml("綁定成功", $"歡迎！已建立帳號<br>Name: {displayName}");
            }
            else
            {
                // 更新最後登入時間
                existingUser.LastLoginAt = DateTime.UtcNow;
                existingUser.Name = displayName; // 更新名稱（以防使用者改名）
                await _context.SaveChangesAsync();

                _logger.LogInformation("使用者登入：LineId={LineId}, Name={Name}", lineUserId, displayName);

                return GenerateSuccessHtml("登入成功", $"歡迎回來！<br>Name: {displayName}");
            }
        }

        private ContentResult GenerateSuccessHtml(string title, string message)
        {
            var html = $@"
<!DOCTYPE html>
<html lang='zh-TW'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{title}</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }}
        .container {{
            background: white;
            padding: 40px;
            border-radius: 10px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            text-align: center;
            max-width: 400px;
        }}
        .success-icon {{
            font-size: 60px;
            color: #4CAF50;
            margin-bottom: 20px;
        }}
        h1 {{
            color: #333;
            margin-bottom: 10px;
            font-size: 24px;
        }}
        p {{
            color: #666;
            line-height: 1.6;
            margin-bottom: 20px;
        }}
        .info {{
            background: #f5f5f5;
            padding: 15px;
            border-radius: 5px;
            margin-bottom: 20px;
        }}
        .countdown {{
            color: #667eea;
            font-weight: bold;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='success-icon'>✓</div>
        <h1>{title}</h1>
        <div class='info'>
            <p>{message}</p>
        </div>
        <p>視窗將在 <span class='countdown' id='countdown'>3</span> 秒後自動關閉...</p>
    </div>
    <script>
        let seconds = 3;
        const countdownElement = document.getElementById('countdown');

        const interval = setInterval(() => {{
            seconds--;
            if (countdownElement) {{
                countdownElement.textContent = seconds;
            }}

            if (seconds <= 0) {{
                clearInterval(interval);
                // 嘗試關閉視窗
                window.close();

                // 如果 window.close() 無效（某些瀏覽器不允許關閉非腳本開啟的視窗）
                // 顯示提示訊息
                setTimeout(() => {{
                    document.body.innerHTML = `
                        <div class='container'>
                            <div class='success-icon'>✓</div>
                            <h1>完成</h1>
                            <p>請手動關閉此視窗</p>
                        </div>
                    `;
                }}, 500);
            }}
        }}, 1000);
    </script>
</body>
</html>";

            return Content(html, "text/html");
        }
    }
}
