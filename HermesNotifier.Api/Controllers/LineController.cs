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

                    // 發送最新一批商品資訊給新用戶
                    await SendLatestProductsToUserAsync(request.LineId);

                    isNewUser = true;
                    message = "歡迎！已建立帳號<br>您可享有 30 日試用期";
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

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("LINE Token API 失敗 - StatusCode: {StatusCode}", response.StatusCode);
                return BadRequest("LINE 授權失敗，請重試");
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
                _logger.LogError(ex, "無法解析 LINE Token 回應");
                return BadRequest("LINE 授權失敗，請重試");
            }

            if (tokenResponse?.AccessToken == null)
            {
                _logger.LogError("Access Token 為空");
                return BadRequest("LINE 授權失敗，請重試");
            }

            // 使用 Access Token 呼叫 LINE Profile API 取得真正的 User ID
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

            var profileResponse = await httpClient.GetAsync("https://api.line.me/v2/profile");

            if (!profileResponse.IsSuccessStatusCode)
            {
                _logger.LogError("無法取得 LINE Profile");
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
                            text = "帳號已成功綁定。\n您可享有 30 日試用期，試用結束後如需續用，請聯繫客服。"
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

        private async Task SendLatestProductsToUserAsync(string lineUserId)
        {
            try
            {
                var channelAccessToken = _config["Line:ChannelAccessToken"]
                    ?? throw new InvalidOperationException("Line:ChannelAccessToken is missing");

                // 獲取最新一批上架的商品（通過最新的 Available log）
                var latestAvailableLog = await _context.ProductLogs
                    .Where(log => log.Action == "Available")
                    .OrderByDescending(log => log.LoggedAt)
                    .FirstOrDefaultAsync();

                if (latestAvailableLog == null)
                {
                    _logger.LogInformation("沒有找到最新的商品上架記錄，跳過發送商品資訊");
                    return;
                }

                // 獲取同一時間批次上架的所有商品
                var latestLogTime = latestAvailableLog.LoggedAt;
                var timeThreshold = latestLogTime.AddMinutes(-5); // 5分鐘內的視為同一批次

                var latestProductIds = await _context.ProductLogs
                    .Where(log => log.Action == "Available" && log.LoggedAt >= timeThreshold && log.LoggedAt <= latestLogTime)
                    .Select(log => log.ProductId)
                    .ToListAsync();

                if (!latestProductIds.Any())
                {
                    _logger.LogInformation("沒有找到最新批次的商品，跳過發送商品資訊");
                    return;
                }

                // 獲取這些商品的詳細資訊
                var products = await _context.Products
                    .Where(p => latestProductIds.Contains(p.Id) && p.IsAvailable)
                    .ToListAsync();

                if (!products.Any())
                {
                    _logger.LogInformation("找到的商品已下架，跳過發送商品資訊");
                    return;
                }

                _logger.LogInformation("準備發送 {count} 個最新商品給新用戶：LineId={LineId}", products.Count, lineUserId);

                const string hermesUrl = "https://www.hermes.com/tw/zh/";
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", channelAccessToken);

                // 將商品分批，每次最多12個
                var productBatches = products.Chunk(12).ToArray();

                foreach (var batch in productBatches)
                {
                    var bubbles = batch.Select(p =>
                    {
                        var lineTargetUrl = string.IsNullOrWhiteSpace(p.ProductUrl) 
                            ? hermesUrl 
                            : p.ProductUrl;

                        var bubble = new Dictionary<string, object>
                        {
                            ["type"] = "bubble"
                        };

                        // 如果有圖片，加上 hero 區塊
                        if (!string.IsNullOrWhiteSpace(p.ImageUrl))
                        {
                            bubble["hero"] = new
                            {
                                type = "image",
                                size = "full",
                                aspectRatio = "1:1",
                                aspectMode = "cover",
                                url = p.ImageUrl,
                                action = new
                                {
                                    type = "uri",
                                    uri = lineTargetUrl
                                }
                            };
                        }

                        // body 區塊
                        bubble["body"] = new
                        {
                            type = "box",
                            layout = "vertical",
                            spacing = "md",
                            contents = new object[]
                            {
                                new
                                {
                                    type = "text",
                                    text = p.Title,
                                    weight = "bold",
                                    wrap = true,
                                    size = "sm"
                                },
                                new
                                {
                                    type = "text",
                                    text = $"NT$ {p.Price:N0}",
                                    color = "#999999",
                                    size = "xs"
                                },
                                new
                                {
                                    type = "text",
                                    text = p.Color ?? "",
                                    color = "#666666",
                                    size = "xs"
                                }
                            }
                        };

                        // 如果沒有圖片，在 body 加上點擊連結的 action
                        if (string.IsNullOrWhiteSpace(p.ImageUrl))
                        {
                            bubble["action"] = new
                            {
                                type = "uri",
                                uri = lineTargetUrl
                            };
                        }

                        return bubble;
                    }).ToList();

                    var flexMessage = new
                    {
                        type = "flex",
                        altText = $"Hermès 最新商品 - 共 {products.Count} 件",
                        contents = new
                        {
                            type = "carousel",
                            contents = bubbles
                        }
                    };

                    var message = new
                    {
                        to = lineUserId,
                        messages = new[] { flexMessage }
                    };

                    var response = await client.PostAsJsonAsync("https://api.line.me/v2/bot/message/push", message);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("最新商品發送成功：LineId={LineId}, 商品數={ProductCount}", 
                            lineUserId, batch.Count());
                    }
                    else
                    {
                        _logger.LogError("最新商品發送失敗：LineId={LineId}, Status={Status}, Response={Response}", 
                            lineUserId, response.StatusCode, responseBody);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送最新商品時發生錯誤：LineId={LineId}", lineUserId);
            }
        }
    }
}
