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
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request)
    {
        var idToken = request.IdToken?.Trim();

        if (string.IsNullOrWhiteSpace(idToken))
        {
            return BadRequest(new { Message = "請提供 LINE id_token。" });
        }

        var lineChannelId = _configuration["Line:ChannelId"];
        var jwtSecret = _configuration["ADMIN_JWT_SECRET"];

        if (string.IsNullOrWhiteSpace(lineChannelId) || string.IsNullOrWhiteSpace(jwtSecret))
        {
            return StatusCode(500, new { Message = "伺服器管理員驗證設定不完整。" });
        }

        string? lineId;
        string? displayName;
        using (var httpClient = new HttpClient())
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
