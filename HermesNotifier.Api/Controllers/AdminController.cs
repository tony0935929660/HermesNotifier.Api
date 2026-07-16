using HermesNotifier.Api.Data;
using HermesNotifier.Api.DTOs.Requests.Admin;
using HermesNotifier.Api.DTOs.Responses.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HermesNotifier.Api.Controllers;

[Route("api/admin")]
[ApiController]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ControllerBase
{
    private const string StatusInStock = "InStock";
    private const string StatusOutOfStock = "OutOfStock";
    private const string StatusNotFound = "NotFound";

    private const string CategoryBags = "包款";
    private const string CategorySmallLeather = "小皮件";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ApplicationDbContext context, ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
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
}
