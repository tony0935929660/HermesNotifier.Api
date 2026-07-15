using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HermesNotifier.Api.Data;
using HermesNotifier.Api.DTOs.Requests.Admin;
using HermesNotifier.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace HermesNotifier.Api.Controllers;

[Route("api/admin/auth")]
[ApiController]
public class AdminAuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AdminAuthController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpGet("line/authorize-url")]
    public IActionResult GetLineAuthorizeUrl([FromQuery] string redirectUri)
    {
        var lineChannelId = _configuration["Line:ChannelId"];
        if (string.IsNullOrWhiteSpace(lineChannelId))
        {
            return StatusCode(500, new { Message = "伺服器 LINE 設定不完整。" });
        }

        if (string.IsNullOrWhiteSpace(redirectUri)
            || !Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return BadRequest(new { Message = "redirectUri 格式錯誤。" });
        }

        var state = Guid.NewGuid().ToString("N");
        var authorizeUrl =
            $"https://access.line.me/oauth2/v2.1/authorize?response_type=code&client_id={Uri.EscapeDataString(lineChannelId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={Uri.EscapeDataString(state)}&scope=profile%20openid";

        return Ok(new
        {
            AuthorizeUrl = authorizeUrl,
            State = state
        });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request)
    {
        var idToken = request.IdToken?.Trim();
        var code = request.Code?.Trim();
        var redirectUri = request.RedirectUri?.Trim();

        var lineChannelId = _configuration["Line:ChannelId"];
        var lineChannelSecret = _configuration["Line:ChannelSecret"];
        var jwtSecret = _configuration["ADMIN_JWT_SECRET"];

        if (string.IsNullOrWhiteSpace(lineChannelId)
            || string.IsNullOrWhiteSpace(lineChannelSecret)
            || string.IsNullOrWhiteSpace(jwtSecret))
        {
            return StatusCode(500, new { Message = "伺服器管理員驗證設定不完整。" });
        }

        string? lineId;
        string? displayName;
        using (var httpClient = new HttpClient())
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                if (string.IsNullOrWhiteSpace(redirectUri))
                {
                    return BadRequest(new { Message = "請提供 RedirectUri。" });
                }

                var tokenForm = new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "code", code },
                    { "redirect_uri", redirectUri },
                    { "client_id", lineChannelId },
                    { "client_secret", lineChannelSecret }
                };

                var tokenResponse = await httpClient.PostAsync(
                    "https://api.line.me/oauth2/v2.1/token",
                    new FormUrlEncodedContent(tokenForm));

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    return Unauthorized(new { Message = "LINE 登入驗證失敗。" });
                }

                var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
                string? lineAccessToken;
                try
                {
                    using var tokenDoc = JsonDocument.Parse(tokenBody);
                    lineAccessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();
                }
                catch
                {
                    return Unauthorized(new { Message = "LINE 登入驗證失敗。" });
                }

                if (string.IsNullOrWhiteSpace(lineAccessToken))
                {
                    return Unauthorized(new { Message = "LINE 登入驗證失敗。" });
                }

                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", lineAccessToken);

                var profileResponse = await httpClient.GetAsync("https://api.line.me/v2/profile");
                if (!profileResponse.IsSuccessStatusCode)
                {
                    return Unauthorized(new { Message = "LINE 登入驗證失敗。" });
                }

                var profileBody = await profileResponse.Content.ReadAsStringAsync();
                try
                {
                    using var profileDoc = JsonDocument.Parse(profileBody);
                    lineId = profileDoc.RootElement.GetProperty("userId").GetString();
                    displayName = profileDoc.RootElement.TryGetProperty("displayName", out var displayNameProp)
                        ? displayNameProp.GetString()
                        : null;
                }
                catch
                {
                    return Unauthorized(new { Message = "LINE 登入驗證失敗。" });
                }
            }
            else if (!string.IsNullOrWhiteSpace(idToken))
            {
                var form = new Dictionary<string, string>
                {
                    { "id_token", idToken },
                    { "client_id", lineChannelId }
                };

                var verifyResponse = await httpClient.PostAsync(
                    "https://api.line.me/oauth2/v2.1/verify",
                    new FormUrlEncodedContent(form));

                if (!verifyResponse.IsSuccessStatusCode)
                {
                    return Unauthorized(new { Message = "LINE 登入驗證失敗。" });
                }

                var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
                try
                {
                    using var doc = JsonDocument.Parse(verifyBody);
                    lineId = doc.RootElement.GetProperty("sub").GetString();
                    displayName = doc.RootElement.TryGetProperty("name", out var nameProp)
                        ? nameProp.GetString()
                        : null;
                }
                catch
                {
                    return Unauthorized(new { Message = "LINE 登入驗證失敗。" });
                }
            }
            else
            {
                return BadRequest(new { Message = "請提供 Code 或 IdToken。" });
            }
        }

        if (string.IsNullOrWhiteSpace(lineId))
        {
            return Unauthorized(new { Message = "LINE 登入驗證失敗。" });
        }

        var adminUser = await _context.Users
            .FirstOrDefaultAsync(u => u.LineId == lineId && u.IsAdmin);

        if (adminUser is null)
        {
            return StatusCode(403, new { Message = "此帳號沒有管理員權限。" });
        }

        var changed = false;
        if (!string.IsNullOrWhiteSpace(displayName) && adminUser.Name != displayName)
        {
            adminUser.Name = displayName;
            changed = true;
        }

        adminUser.LastLoginAt = TaiwanTime.Now;
        if (!changed)
        {
            // 即便名稱沒變，也更新最後登入時間。
            changed = true;
        }

        if (changed)
        {
            await _context.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;
        var expiresAt = now.AddHours(8);

        var claims = new List<Claim>
        {
            new("lineId", lineId),
            new(ClaimTypes.NameIdentifier, lineId),
            new(ClaimTypes.Role, "Admin")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresAt = expiresAt
        });
    }
}
