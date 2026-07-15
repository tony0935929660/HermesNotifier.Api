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

        var items = await query
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
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
            TotalCount = items.Count,
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

        var items = await products
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
            .OrderByDescending(x => x.LatestLog!.LoggedAt)
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
            TotalCount = items.Count,
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
