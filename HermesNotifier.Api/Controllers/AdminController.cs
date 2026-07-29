using HermesNotifier.Api.Data;
using HermesNotifier.Api.DTOs.Requests.Admin;
using HermesNotifier.Api.DTOs.Responses.Admin;
using HermesNotifier.Api.Infrastructure;
using HermesNotifier.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace HermesNotifier.Api.Controllers;

[Route("api/admin")]
[ApiController]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ControllerBase
{
    private sealed record LineBroadcastResult(int AttemptedUsers, int SuccessUsers, int FailedUsers, int SuccessBatches);

    private const string StatusInStock = "InStock";
    private const string StatusOutOfStock = "OutOfStock";
    private const string StatusNotFound = "NotFound";
    private const string LevelA = "A";
    private const string LevelB = "B";
    private const string LevelC = "C";
    private const string LevelD = "D";
    private const string LevelE = "E";

    private const string CategoryBags = "包款";
    private const string CategorySmallLeather = "小皮件";
    private const string HermesHostSuffix = ".hermes.com";
    private const string WebUnlockerEndpoint = "https://api.brightdata.com/request";

    private static readonly Regex ProductIdInUrlRegex = new(@"/product/(?:[^/]*-)?(?<sku>H[A-Z0-9]{10})(?:/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NameRegex = new("\"name\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PriceRegex = new("\"price\"\\s*:\\s*\"?(?<value>[0-9]+(?:\\.[0-9]+)?)\"?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ImageRegex = new("\"image\"\\s*:\\s*\"(?<value>https?://[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ColorRegex = new("\"color\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AvailabilityRegex = new("\"availability\"\\s*:\\s*\"[^\"]*/(?<value>InStock|OutOfStock|NotFound)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminController> _logger;
    private readonly IConfiguration _config;

    public AdminController(ApplicationDbContext context, ILogger<AdminController> logger, IConfiguration config)
    {
        _context = context;
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// 管理員查詢商品。
    /// 篩選：分類、關鍵字（名稱/產品ID）、狀態。
    /// </summary>
    [HttpGet("products")]
    public async Task<ActionResult<AdminProductQueryResponse>> QueryProducts([FromQuery] AdminProductQueryRequest request)
    {
        var query = _context.Products.AsNoTracking().AsQueryable();

        if (!TryNormalizeCategory(request.Category, out var category, out var categoryError))
        {
            return BadRequest(new { Message = categoryError });
        }

        if (!TryNormalizeStatus(request.Status, out var status, out var statusError))
        {
            return BadRequest(new { Message = statusError });
        }

        var keyword = request.Keyword?.Trim();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(p => p.Title.Contains(keyword) || p.ProductId.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => p.AvailabilityStatus == status);
        }

        var totalCount = await query.CountAsync();
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);

        var items = await query
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new AdminProductItemDto
            {
                ProductId = p.ProductId,
                Name = p.Title,
                Price = p.Price,
                Color = p.Color,
                ProductUrl = p.ProductUrl,
                Type = p.Category,
                Level = p.Level,
                Status = p.AvailabilityStatus
            })
            .ToListAsync();

        _logger.LogInformation(
            "管理員查詢商品：category={category}, keyword={keyword}, status={status}, count={count}",
            category ?? "(全部)",
            keyword ?? "(無)",
            status ?? "(全部)",
            items.Count);

        return Ok(new AdminProductQueryResponse
        {
            TotalCount = totalCount,
            Items = items
        });
    }

    /// <summary>
    /// 管理員查詢每個商品的最新狀態記錄。
    /// 篩選：關鍵字（名稱/產品ID）。
    /// </summary>
    [HttpGet("logs")]
    public async Task<ActionResult<AdminLogQueryResponse>> QueryLogs([FromQuery] AdminLogQueryRequest request)
    {
        var products = _context.Products.AsNoTracking().AsQueryable();
        var keyword = request.Keyword?.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            products = products.Where(p => p.Title.Contains(keyword) || p.ProductId.Contains(keyword));
        }

        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);

        var query = products
            .Select(p => new
            {
                p.ProductId,
                p.Title,
                LatestLog = _context.ProductLogs
                    .Where(l => l.ProductId == p.Id)
                    .OrderByDescending(l => l.LoggedAt)
                    .ThenByDescending(l => l.Id)
                    .Select(l => new { l.Action, l.LoggedAt })
                    .FirstOrDefault()
            })
            .Where(x => x.LatestLog != null)
            .OrderByDescending(x => x.LatestLog!.LoggedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminLogItemDto
            {
                ProductId = x.ProductId,
                Name = x.Title,
                LatestStatus = ConvertLogActionToStatus(x.LatestLog!.Action),
                LoggedAt = x.LatestLog!.LoggedAt
            })
            .ToListAsync();

        _logger.LogInformation("管理員查詢 LOG：keyword={keyword}, count={count}", keyword ?? "(無)", items.Count);

        return Ok(new AdminLogQueryResponse
        {
            TotalCount = totalCount,
            Items = items
        });
    }

    [HttpGet("availability-logs")]
    public async Task<ActionResult<AdminAvailabilityLogQueryResponse>> QueryAvailabilityLogs([FromQuery] AdminAvailabilityLogQueryRequest request)
    {
        var logs = _context.ProductLogs
            .AsNoTracking()
            .Join(
                _context.Products.AsNoTracking(),
                log => log.ProductId,
                product => product.Id,
                (log, product) => new
                {
                    product.ProductId,
                    product.Title,
                    log.Action,
                    log.LoggedAt
                });

        var keyword = request.Keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            logs = logs.Where(item => item.Title.Contains(keyword) || item.ProductId.Contains(keyword));
        }

        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);

        var totalCount = await logs.CountAsync();
        var items = await logs
            .OrderByDescending(item => item.LoggedAt)
            .ThenByDescending(item => item.ProductId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AdminAvailabilityLogItemDto
            {
                ProductId = item.ProductId,
                Name = item.Title,
                Action = item.Action,
                Status = ConvertLogActionToStatus(item.Action),
                LoggedAt = item.LoggedAt
            })
            .ToListAsync();

        return Ok(new AdminAvailabilityLogQueryResponse
        {
            TotalCount = totalCount,
            Items = items
        });
    }

    [HttpGet("product-history")]
    public async Task<ActionResult<AdminProductHistoryQueryResponse>> QueryProductHistory([FromQuery] AdminProductHistoryQueryRequest request)
    {
        var productId = request.ProductId?.Trim();
        if (string.IsNullOrWhiteSpace(productId))
        {
            return BadRequest(new { Message = "請提供 productId。" });
        }

        var product = await _context.Products
            .AsNoTracking()
            .Where(p => p.ProductId == productId)
            .Select(p => new { p.Id, p.ProductId, p.Title })
            .FirstOrDefaultAsync();

        if (product is null)
        {
            return NotFound(new { Message = $"找不到產品 ID: {productId}" });
        }

        var items = await _context.ProductLogs
            .AsNoTracking()
            .Where(log => log.ProductId == product.Id)
            .OrderByDescending(log => log.LoggedAt)
            .ThenByDescending(log => log.Id)
            .Select(log => new AdminProductHistoryItemDto
            {
                Action = log.Action,
                Status = ConvertLogActionToStatus(log.Action),
                LoggedAt = log.LoggedAt
            })
            .ToListAsync();

        return Ok(new AdminProductHistoryQueryResponse
        {
            ProductId = product.ProductId,
            ProductName = product.Title,
            TotalCount = items.Count,
            Items = items
        });
    }

    [HttpGet("users")]
    [HttpGet("members")]
    public async Task<ActionResult<AdminUserQueryResponse>> QueryUsers([FromQuery] AdminUserQueryRequest request)
    {
        var query = _context.Users.AsNoTracking().AsQueryable();
        var keyword = request.Keyword?.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(user => (user.Name != null && user.Name.Contains(keyword)) || user.LineId.Contains(keyword));
        }

        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(user => user.IsAdmin)
            .ThenByDescending(user => user.SubscribedUntil)
            .ThenByDescending(user => user.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new AdminUserItemDto
            {
                LineId = user.LineId,
                Name = user.Name,
                IsAdmin = user.IsAdmin,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                LastLoginAt = user.LastLoginAt,
                SubscribedUntil = user.SubscribedUntil
            })
            .ToListAsync();

        return Ok(new AdminUserQueryResponse
        {
            TotalCount = totalCount,
            Items = items
        });
    }

    [HttpPatch("products/{productId}/level")]
    public async Task<ActionResult> UpdateProductLevel(string productId, [FromBody] AdminUpdateProductLevelRequest request)
    {
        if (!TryNormalizeLevel(request.Level, out var normalizedLevel))
        {
            return BadRequest(new { Message = "level 僅支援 A/B/C/D/E。" });
        }

        var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == productId);
        if (product is null)
        {
            return NotFound(new { Message = $"找不到產品 ID: {productId}" });
        }

        if (product.Level == normalizedLevel)
        {
            return Ok(new
            {
                Message = "Level 無異動",
                ProductId = product.ProductId,
                Level = product.Level,
                Changed = false
            });
        }

        product.Level = normalizedLevel;
        product.UpdatedAt = TaiwanTime.Now;
        await _context.SaveChangesAsync();

        _logger.LogInformation("管理員更新產品 Level：productId={productId}, level={level}", productId, normalizedLevel);

        return Ok(new
        {
            Message = "Level 更新成功",
            ProductId = product.ProductId,
            Level = product.Level,
            Changed = true,
            UpdatedAt = product.UpdatedAt
        });
    }

    [HttpPost("products/import-by-url")]
    public async Task<ActionResult> ImportProductByUrl([FromBody] AdminImportProductByUrlRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProductUrl))
        {
            return BadRequest(new { Message = "請提供商品網址。" });
        }

        if (!Uri.TryCreate(request.ProductUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !(uri.Host.Equals("hermes.com", StringComparison.OrdinalIgnoreCase)
                 || uri.Host.EndsWith(HermesHostSuffix, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new { Message = "只允許匯入 hermes.com 的商品網址。" });
        }

        var productIdMatch = ProductIdInUrlRegex.Match(uri.AbsolutePath);
        if (!productIdMatch.Success)
        {
            return BadRequest(new { Message = "網址格式不正確，無法解析商品 SKU（例如 H086955CC89）。" });
        }

        var productId = productIdMatch.Groups["sku"].Value.ToUpperInvariant();
        var sourceUrl = $"https://www.hermes.com/tw/zh/product/{productId}/";

        var apiKey = _config["WEB_UNLOCKER_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return StatusCode(500, new { Message = "伺服器未設定 WEB_UNLOCKER_API_KEY。" });
        }

        var zone = _config["WEB_UNLOCKER_ZONE"];
        if (string.IsNullOrWhiteSpace(zone))
        {
            zone = "hermes_unlocker";
        }

        var fetchResult = await FetchHtmlViaWebUnlockerAsync(apiKey, zone, sourceUrl);
        if (!fetchResult.Success)
        {
            return StatusCode(fetchResult.StatusCode, new { Message = fetchResult.ErrorMessage });
        }

        if (!TryParseHermesProduct(fetchResult.Html!, productId, sourceUrl, out var parsed, out var parseError))
        {
            return StatusCode(422, new { Message = parseError });
        }

        var now = TaiwanTime.Now;
        var existing = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == productId);
        var isNew = existing is null;
        var becameInStock = false;
        var changed = false;

        if (isNew)
        {
            existing = new Product
            {
                ProductId = productId,
                Title = parsed.Title,
                Price = parsed.Price,
                ImageUrl = parsed.ImageUrl,
                ProductUrl = parsed.ProductUrl,
                Color = parsed.Color,
                Category = CategoryBags,
                Level = ResolveLevelForUpsert(null, parsed.Title),
                IsAvailable = parsed.IsAvailable,
                AvailabilityStatus = parsed.AvailabilityStatus,
                CacheExpiresAt = null,
                CreatedAt = now,
                UpdatedAt = now
            };
            becameInStock = parsed.AvailabilityStatus == StatusInStock;
            changed = true;
            await _context.Products.AddAsync(existing);
        }
        else
        {
            var oldStatus = existing!.AvailabilityStatus;

            if (existing.Title != parsed.Title)
            {
                existing.Title = parsed.Title;
                changed = true;
            }

            if (existing.Price != parsed.Price)
            {
                existing.Price = parsed.Price;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(parsed.ImageUrl) && existing.ImageUrl != parsed.ImageUrl)
            {
                existing.ImageUrl = parsed.ImageUrl;
                changed = true;
            }

            if (existing.ProductUrl != parsed.ProductUrl)
            {
                existing.ProductUrl = parsed.ProductUrl;
                changed = true;
            }

            if (existing.Color != parsed.Color)
            {
                existing.Color = parsed.Color;
                changed = true;
            }

            var inferredLevel = ResolveLevelForUpsert(null, parsed.Title);
            if (existing.Level != inferredLevel)
            {
                existing.Level = inferredLevel;
                changed = true;
            }

            if (existing.AvailabilityStatus != parsed.AvailabilityStatus || existing.IsAvailable != parsed.IsAvailable)
            {
                existing.AvailabilityStatus = parsed.AvailabilityStatus;
                existing.IsAvailable = parsed.IsAvailable;
                changed = true;
            }

            becameInStock = oldStatus != StatusInStock && parsed.AvailabilityStatus == StatusInStock;
            if (changed)
            {
                existing.UpdatedAt = now;
            }
        }

        if (changed)
        {
            await _context.SaveChangesAsync();

            await _context.ProductLogs.AddAsync(new ProductLog
            {
                ProductId = existing!.Id,
                Action = GetProductLogAction(parsed.AvailabilityStatus),
                LoggedAt = now
            });
            await _context.SaveChangesAsync();
        }

        LineBroadcastResult? notification = null;
        if (parsed.AvailabilityStatus == StatusInStock && (isNew || becameInStock))
        {
            notification = await BroadcastLineMessageAsync(new List<Product> { existing! });
        }

        return Ok(new
        {
            Message = isNew ? "商品匯入成功" : "商品覆蓋更新成功",
            ProductId = existing!.ProductId,
            ProductUrl = existing.ProductUrl,
            Level = existing.Level,
            AvailabilityStatus = existing.AvailabilityStatus,
            IsAvailable = existing.IsAvailable,
            Changed = changed,
            IsNew = isNew,
            BecameInStock = becameInStock,
            Notification = notification is null
                ? null
                : new
                {
                    AttemptedUsers = notification.AttemptedUsers,
                    SuccessUsers = notification.SuccessUsers,
                    FailedUsers = notification.FailedUsers,
                    SuccessBatches = notification.SuccessBatches
                }
        });
    }

    private static string ConvertLogActionToStatus(string action)
    {
        return action.Trim().ToLowerInvariant() switch
        {
            "available" => StatusInStock,
            "unavailable" => StatusOutOfStock,
            "notfound" => StatusNotFound,
            _ => StatusOutOfStock
        };
    }

    private static int NormalizePage(int page)
    {
        return page < 1 ? 1 : page;
    }

    private static int NormalizePageSize(int pageSize)
    {
        if (pageSize < 1)
        {
            return 10;
        }

        return pageSize > 100 ? 100 : pageSize;
    }

    private static bool TryNormalizeCategory(string? input, out string? normalized, out string errorMessage)
    {
        normalized = null;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        var value = input.Trim().ToLowerInvariant();
        switch (value)
        {
            case "包款":
            case "bag":
            case "bags":
                normalized = CategoryBags;
                return true;

            case "小皮件":
            case "smallleathergoods":
            case "small-leather-goods":
            case "slg":
                normalized = CategorySmallLeather;
                return true;

            default:
                errorMessage = "category 僅支援 包款 或 小皮件。";
                return false;
        }
    }

    private static bool TryNormalizeStatus(string? input, out string? normalized, out string errorMessage)
    {
        normalized = null;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        var value = input.Trim().ToLowerInvariant();
        switch (value)
        {
            case "instock":
            case "in_stock":
            case "available":
            case "true":
                normalized = StatusInStock;
                return true;

            case "outofstock":
            case "out_of_stock":
            case "unavailable":
            case "false":
                normalized = StatusOutOfStock;
                return true;

            case "notfound":
            case "not_found":
            case "404":
                normalized = StatusNotFound;
                return true;

            default:
                errorMessage = "status 僅支援 InStock / OutOfStock / NotFound。";
                return false;
        }
    }

    private static bool TryNormalizeLevel(string? input, out string normalized)
    {
        normalized = LevelC;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var value = input.Trim().ToUpperInvariant();
        if (value is LevelA or LevelB or LevelC or LevelD or LevelE)
        {
            normalized = value;
            return true;
        }

        return false;
    }

    private static string GetProductLogAction(string status)
    {
        return status switch
        {
            StatusInStock => "Available",
            StatusOutOfStock => "Unavailable",
            StatusNotFound => "NotFound",
            _ => "Unavailable"
        };
    }

    private static string ResolveLevelForUpsert(string? inputLevel, string? title)
    {
        if (TryNormalizeLevel(inputLevel, out var normalized))
        {
            return normalized;
        }

        if (TryInferLevelFromTitle(title, out var inferred))
        {
            return inferred;
        }

        return LevelC;
    }

    private static bool TryInferLevelFromTitle(string? title, out string level)
    {
        level = LevelC;
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var t = title.Trim();

        if (ContainsAny(t, "Picotin Lock 18", "Evelyne 16 Amazone", "Roulis", "Lindy 26", "Halzan迷你", "In-the-Loop 18"))
        {
            level = LevelA;
            return true;
        }

        if (ContainsAny(t,
            "Picotin Lock 22",
            "Evelyne 23 Poche III",
            "24/24 - 21",
            "Garden Party 30",
            "Herbag Zip 20",
            "Herbag Zip 31",
            "Halzan 25",
            "Kelly depeches 25",
            "Kelly郵差包",
            "Jypsiere迷你",
            "Geta Slim",
            "Neo Garden 23",
            "Evelyne III 29"))
        {
            level = LevelB;
            return true;
        }

        if (ContainsAny(t,
            "Poche Cliquetis",
            "Videpoches",
            "So Medor",
            "Neo Medor",
            "Steve light junior",
            "Sac a depeches 21",
            "Sac a depeches light 1-36",
            "Bolide",
            "Le Petit Sac",
            "Steeple 25",
            "Steeple 28",
            "Maximors II",
            "Maximors",
            "Hac a Dos PM",
            "Hac a Dos GM",
            "Jypsiere mini Toile & Cuir"))
        {
            level = LevelC;
            return true;
        }

        if (ContainsAny(t,
            "Cab'H",
            "Medor手提包",
            "En Piste",
            "Tout en Carre",
            "Balusoie",
            "Fonsbelle Chaine",
            "Herbag Messenger 39",
            "Harnacheur",
            "Onbody Etriviere",
            "Collier d'Attelage",
            "Della Cavalleria Elan",
            "Messenger 57"))
        {
            level = LevelD;
            return true;
        }

        if (ContainsAny(t, "Sanglons", "Lassoie"))
        {
            level = LevelE;
            return true;
        }

        return false;
    }

    private static bool ContainsAny(string source, params string[] patterns)
    {
        foreach (var p in patterns)
        {
            if (source.Contains(p, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<LineBroadcastResult> BroadcastLineMessageAsync(List<Product> products)
    {
        var token = _config.GetLineChannelAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Line:ChannelAccessToken / LINE_BOT_CHANNEL_ACCESS_TOKEN 未設定，無法使用 LINE Messaging API 廣播。");
            return new LineBroadcastResult(0, 0, 0, 0);
        }

        try
        {
            var now = TaiwanTime.Now;
            var activeSubscribers = await _context.Users
                .Where(u => u.SubscribedUntil.HasValue && u.SubscribedUntil.Value > now)
                .ToListAsync();

            if (!activeSubscribers.Any())
            {
                _logger.LogInformation("沒有訂閱中的使用者，跳過通知發送");
                return new LineBroadcastResult(0, 0, 0, 0);
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var attemptedUsers = activeSubscribers.Count;
            var successUsers = 0;
            var failedUsers = 0;
            var successBatches = 0;
            var productBatches = products.Chunk(12).ToArray();

            foreach (var batch in productBatches)
            {
                var bubbles = batch.Select(p =>
                {
                    var lineTargetUrl = string.IsNullOrWhiteSpace(p.ProductUrl)
                        ? "https://www.hermes.com/tw/zh/"
                        : p.ProductUrl;

                    var bubble = new Dictionary<string, object> { ["type"] = "bubble" };
                    if (!string.IsNullOrWhiteSpace(p.ImageUrl))
                    {
                        bubble["hero"] = new
                        {
                            type = "image",
                            size = "full",
                            aspectRatio = "1:1",
                            aspectMode = "cover",
                            url = p.ImageUrl,
                            action = new { type = "uri", uri = lineTargetUrl }
                        };
                    }

                    bubble["body"] = new
                    {
                        type = "box",
                        layout = "vertical",
                        spacing = "md",
                        contents = new object[]
                        {
                            new { type = "text", text = p.Title, weight = "bold", wrap = true, size = "sm" },
                            new { type = "text", text = $"NT$ {p.Price:N0}", color = "#999999", size = "xs" },
                            new { type = "text", text = p.Color ?? string.Empty, color = "#666666", size = "xs" }
                        }
                    };

                    if (string.IsNullOrWhiteSpace(p.ImageUrl))
                    {
                        bubble["action"] = new { type = "uri", uri = lineTargetUrl };
                    }

                    return bubble;
                }).ToList();

                var flexMessage = new
                {
                    type = "flex",
                    altText = $"Hermès 商品上架通知 - 共 {products.Count} 件商品",
                    contents = new
                    {
                        type = "carousel",
                        contents = bubbles
                    }
                };

                foreach (var userBatch in activeSubscribers.Select(u => u.LineId).Chunk(500))
                {
                    var payload = new { to = userBatch.ToArray(), messages = new[] { flexMessage } };
                    var response = await client.PostAsJsonAsync("https://api.line.me/v2/bot/message/multicast", payload);

                    if (response.IsSuccessStatusCode)
                    {
                        successUsers += userBatch.Length;
                        successBatches += 1;
                    }
                    else
                    {
                        failedUsers += userBatch.Length;
                    }
                }
            }

            return new LineBroadcastResult(attemptedUsers, successUsers, failedUsers, successBatches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LINE multicast 發送失敗");
            return new LineBroadcastResult(0, 0, 0, 0);
        }
    }

    private async Task<(bool Success, int StatusCode, string? Html, string? ErrorMessage)> FetchHtmlViaWebUnlockerAsync(
        string apiKey,
        string zone,
        string url)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                zone,
                url,
                format = "raw"
            };

            var response = await client.PostAsJsonAsync(WebUnlockerEndpoint, payload);
            var html = await response.Content.ReadAsStringAsync();
            var brdError = response.Headers.TryGetValues("x-brd-error", out var values)
                ? values.FirstOrDefault()
                : null;

            if (!response.IsSuccessStatusCode)
            {
                return (false, (int)response.StatusCode, null, $"Web Unlocker 失敗：{response.StatusCode}");
            }

            if (!string.IsNullOrWhiteSpace(brdError) || string.IsNullOrWhiteSpace(html))
            {
                return (false, 502, null, $"Web Unlocker 未取得有效內容：{brdError ?? "空白回應"}");
            }

            return (true, 200, html, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Web Unlocker 抓取失敗：{url}", url);
            return (false, 500, null, "Web Unlocker 抓取失敗，請稍後再試。");
        }
    }

    private static bool TryParseHermesProduct(
        string html,
        string productId,
        string productUrl,
        out (string Title, decimal Price, string? ImageUrl, string? Color, bool IsAvailable, string AvailabilityStatus, string ProductUrl) result,
        out string error)
    {
        result = default;
        error = string.Empty;

        if (html.Contains("<title>404", StringComparison.OrdinalIgnoreCase) || html.Contains("not-found", StringComparison.OrdinalIgnoreCase))
        {
            error = "商品頁回傳 404/NotFound，無法匯入。";
            return false;
        }

        var title = DecodeJsonText(NameRegex.Match(html).Groups["value"].Value).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            error = "解析失敗：找不到商品名稱。";
            return false;
        }

        var availabilityText = AvailabilityRegex.Match(html).Groups["value"].Value;
        var availabilityStatus = availabilityText.Equals("InStock", StringComparison.OrdinalIgnoreCase)
            ? StatusInStock
            : availabilityText.Equals("OutOfStock", StringComparison.OrdinalIgnoreCase)
                ? StatusOutOfStock
                : StatusNotFound;

        var isAvailable = availabilityStatus == StatusInStock;

        decimal price = 0;
        var priceText = PriceRegex.Match(html).Groups["value"].Value;
        if (!decimal.TryParse(priceText, out price))
        {
            price = 0;
        }

        var imageUrl = DecodeJsonText(ImageRegex.Match(html).Groups["value"].Value);
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            imageUrl = null;
        }

        var color = DecodeJsonText(ColorRegex.Match(html).Groups["value"].Value);
        if (string.IsNullOrWhiteSpace(color))
        {
            color = null;
        }

        result = (title, price, imageUrl, color, isAvailable, availabilityStatus, productUrl);
        return true;
    }

    private static string DecodeJsonText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value
            .Replace("\\u002F", "/", StringComparison.Ordinal)
            .Replace("\\/", "/", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\n", " ", StringComparison.Ordinal)
            .Replace("\\r", " ", StringComparison.Ordinal)
            .Trim();
    }
}
