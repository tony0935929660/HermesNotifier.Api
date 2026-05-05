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
            var liffId = _config["Line:LiffId"] ?? "";

            var html = $@"
<!DOCTYPE html>
<html lang='zh-TW'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>綁定帳號</title>
    <style>
        @import url('https://fonts.googleapis.com/css2?family=Noto+Serif+TC:wght@300;400&display=swap');

        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: 'Noto Serif TC', Georgia, serif;
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            background: #FAF7F2;
            padding: 20px;
        }}

        .container {{
            background: #FFFFFF;
            padding: 80px 50px;
            text-align: center;
            max-width: 500px;
            width: 100%;
            box-shadow: 0 0 40px rgba(0, 0, 0, 0.03);
        }}

        .brand {{
            font-size: 14px;
            letter-spacing: 4px;
            color: #1A1A1A;
            font-weight: 300;
            margin-bottom: 80px;
            text-transform: uppercase;
        }}

        h1 {{
            color: #1A1A1A;
            font-size: 28px;
            font-weight: 300;
            letter-spacing: 2px;
            margin-bottom: 60px;
            line-height: 1.6;
        }}

        .message {{
            color: #4A4A4A;
            font-size: 18px;
            font-weight: 300;
            line-height: 2;
            margin-bottom: 70px;
            letter-spacing: 1px;
        }}

        .loading {{
            display: inline-block;
            width: 50px;
            height: 50px;
            border: 3px solid #E8E8E8;
            border-top-color: #FF6B35;
            border-radius: 50%;
            animation: spin 1s linear infinite;
        }}

        @keyframes spin {{
            to {{ transform: rotate(360deg); }}
        }}

        @media (max-width: 480px) {{
            .container {{
                padding: 60px 30px;
            }}

            h1 {{
                font-size: 24px;
            }}

            .message {{
                font-size: 16px;
            }}
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='brand'>HERMES NOTIFIER</div>
        <div class='loading'></div>
        <h1 id='title'>處理中</h1>
        <div class='message' id='message'>正在綁定您的帳號...</div>
    </div>
    <script src='https://static.line-scdn.net/liff/edge/2/sdk.js'></script>
    <script>
        const liffId = '{liffId}';
        const titleEl = document.getElementById('title');
        const messageEl = document.getElementById('message');

        async function bindAccount() {{
            try {{
                // 初始化 LIFF
                await liff.init({{ liffId: liffId }});

                // 檢查是否已登入
                if (!liff.isLoggedIn()) {{
                    // 如果未登入，導向 LINE 登入
                    liff.login();
                    return;
                }}

                // 取得用戶 Profile
                const profile = await liff.getProfile();

                // 呼叫後端 API 綁定帳號
                const response = await fetch('/api/line/bind-liff', {{
                    method: 'POST',
                    headers: {{
                        'Content-Type': 'application/json',
                    }},
                    body: JSON.stringify({{
                        lineId: profile.userId,
                        displayName: profile.displayName
                    }})
                }});

                const result = await response.json();

                if (response.ok) {{
                    // 綁定成功
                    titleEl.textContent = result.isNewUser ? '綁定成功' : '登入成功';
                    messageEl.innerHTML = result.message;

                    // 3 秒後關閉視窗
                    setTimeout(() => {{
                        liff.closeWindow();
                    }}, 3000);
                }} else {{
                    // 綁定失敗
                    titleEl.textContent = '綁定失敗';
                    messageEl.innerHTML = result.message || '發生錯誤，請稍後再試';
                }}
            }} catch (err) {{
                console.error('LIFF Error:', err);
                titleEl.textContent = '發生錯誤';
                messageEl.innerHTML = '無法連接服務，請稍後再試';
            }}
        }}

        bindAccount();
    </script>
</body>
</html>";

            return Content(html, "text/html");
        }

        [HttpPost("bind-liff")]
        public async Task<IActionResult> BindLiff([FromBody] LiffBindRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.LineId))
                {
                    return BadRequest(new { message = "Line ID 不能為空" });
                }

                // 檢查使用者是否已存在
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.LineId == request.LineId);

                bool isNewUser = false;
                string message;

                if (existingUser == null)
                {
                    // 建立新使用者
                    var newUser = new User
                    {
                        LineId = request.LineId,
                        Name = request.DisplayName,
                        LastLoginAt = DateTime.UtcNow
                    };

                    _context.Users.Add(newUser);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("新增使用者：LineId={LineId}, Name={Name}", request.LineId, request.DisplayName);

                    // 發送歡迎訊息
                    await SendWelcomeMessageAsync(request.LineId);

                    isNewUser = true;
                    message = "歡迎！已建立帳號<br>您可享有 7 日試用期";
                }
                else
                {
                    // 更新最後登入時間
                    existingUser.LastLoginAt = DateTime.UtcNow;
                    existingUser.Name = request.DisplayName;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("使用者登入：LineId={LineId}, Name={Name}", request.LineId, request.DisplayName);

                    // 發送已綁定訊息
                    await SendAlreadyBoundMessageAsync(request.LineId);

                    isNewUser = false;
                    message = "歡迎回來！<br>如需續用，請聯絡客服";
                }

                return Ok(new 
                { 
                    success = true, 
                    isNewUser = isNewUser,
                    message = message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LIFF 綁定失敗");
                return StatusCode(500, new { message = "伺服器錯誤，請稍後再試" });
            }
        }

        public class LiffBindRequest
        {
            public string LineId { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
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

                // 發送歡迎訊息
                await SendWelcomeMessageAsync(lineUserId);

                return GenerateSuccessHtml("綁定成功", $"歡迎！已建立帳號<br>Name: {displayName}");
            }
            else
            {
                // 更新最後登入時間
                existingUser.LastLoginAt = DateTime.UtcNow;
                existingUser.Name = displayName; // 更新名稱（以防使用者改名）
                await _context.SaveChangesAsync();

                _logger.LogInformation("使用者登入：LineId={LineId}, Name={Name}", lineUserId, displayName);

                // 發送已綁定訊息
                await SendAlreadyBoundMessageAsync(lineUserId);

                return GenerateSuccessHtml("登入成功", $"歡迎回來！<br>Name: {displayName}");
            }
        }

        private ContentResult GenerateSuccessHtml(string title, string message)
        {
            var liffId = _config["Line:LiffId"] ?? "";

            var html = $@"
<!DOCTYPE html>
<html lang='zh-TW'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{title}</title>
    <style>
        @import url('https://fonts.googleapis.com/css2?family=Noto+Serif+TC:wght@300;400&display=swap');

        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: 'Noto Serif TC', Georgia, serif;
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            background: #FAF7F2;
            padding: 20px;
        }}

        .container {{
            background: #FFFFFF;
            padding: 80px 50px;
            text-align: center;
            max-width: 500px;
            width: 100%;
            box-shadow: 0 0 40px rgba(0, 0, 0, 0.03);
        }}

        .brand {{
            font-size: 14px;
            letter-spacing: 4px;
            color: #1A1A1A;
            font-weight: 300;
            margin-bottom: 80px;
            text-transform: uppercase;
        }}

        h1 {{
            color: #1A1A1A;
            font-size: 28px;
            font-weight: 300;
            letter-spacing: 2px;
            margin-bottom: 60px;
            line-height: 1.6;
        }}

        .message {{
            color: #4A4A4A;
            font-size: 18px;
            font-weight: 300;
            line-height: 2;
            margin-bottom: 70px;
            letter-spacing: 1px;
        }}

        .countdown-box {{
            border-top: 1px solid #E8E8E8;
            padding-top: 30px;
        }}

        .countdown-text {{
            color: #8A8A8A;
            font-size: 14px;
            letter-spacing: 1px;
            font-weight: 300;
        }}

        .countdown {{
            display: inline-block;
            color: #FF6B35;
            font-size: 20px;
            font-weight: 400;
            min-width: 20px;
            letter-spacing: 0;
        }}

        @media (max-width: 480px) {{
            .container {{
                padding: 60px 30px;
            }}

            h1 {{
                font-size: 24px;
            }}

            .message {{
                font-size: 16px;
            }}
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='brand'>HERMES NOTIFIER</div>
        <h1>{title}</h1>
        <div class='message'>{message}</div>
        <div class='countdown-box'>
            <p class='countdown-text'>視窗將在 <span class='countdown' id='countdown'>3</span> 秒後自動關閉</p>
        </div>
    </div>
    <script>
        // 檢測是否在 LINE 內建瀏覽器中
        const isLineApp = /Line/i.test(navigator.userAgent);

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
                if (isLineApp) {{
                    // 在 LINE 中，嘗試使用 LINE 的關閉方法
                    if (window.LineIt && window.LineIt.closeWindow) {{
                        window.LineIt.closeWindow();
                    }} else {{
                        window.close();
                    }}
                }} else {{
                    // 在外部瀏覽器，嘗試關閉
                    window.close();
                }}

                // 如果無法關閉，顯示提示訊息
                setTimeout(() => {{
                    const container = document.querySelector('.container');
                    if (container) {{
                        container.innerHTML = `
                            <div class='brand'>HERMES NOTIFIER</div>
                            <h1>完成</h1>
                            <div class='countdown-box'>
                                <p class='countdown-text'>請手動關閉此視窗</p>
                            </div>
                        `;
                    }}
                }}, 500);
            }}
        }}, 1000);
    </script>
                        }}
                    }}, 500);
                }}
            }}
        }}, 1000);
    </script>
</body>
</html>";

            return Content(html, "text/html");
        }

        private async Task SendWelcomeMessageAsync(string lineUserId)
        {
            try
            {
                var channelAccessToken = _config["Line:ChannelAccessToken"]
                    ?? throw new InvalidOperationException("Line:ChannelAccessToken is missing");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", channelAccessToken);

                var message = new
                {
                    to = lineUserId,
                    messages = new[]
                    {
                        new
                        {
                            type = "text",
                            text = "帳號已成功綁定。\n您可享有 7 日試用期，試用結束後如需續用，請聯繫客服。"
                        }
                    }
                };

                var response = await client.PostAsJsonAsync("https://api.line.me/v2/bot/message/push", message);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("歡迎訊息發送成功：LineId={LineId}", lineUserId);
                }
                else
                {
                    _logger.LogError("歡迎訊息發送失敗：LineId={LineId}, Status={Status}, Response={Response}", 
                        lineUserId, response.StatusCode, responseBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送歡迎訊息時發生錯誤：LineId={LineId}", lineUserId);
            }
        }

        private async Task SendAlreadyBoundMessageAsync(string lineUserId)
        {
            try
            {
                var channelAccessToken = _config["Line:ChannelAccessToken"]
                    ?? throw new InvalidOperationException("Line:ChannelAccessToken is missing");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", channelAccessToken);

                var message = new
                {
                    to = lineUserId,
                    messages = new[]
                    {
                        new
                        {
                            type = "text",
                            text = "您已經綁定過，如需續用，請聯絡客服。"
                        }
                    }
                };

                var response = await client.PostAsJsonAsync("https://api.line.me/v2/bot/message/push", message);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("已綁定訊息發送成功：LineId={LineId}", lineUserId);
                }
                else
                {
                    _logger.LogError("已綁定訊息發送失敗：LineId={LineId}, Status={Status}, Response={Response}", 
                        lineUserId, response.StatusCode, responseBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送已綁定訊息時發生錯誤：LineId={LineId}", lineUserId);
            }
        }
    }
}
