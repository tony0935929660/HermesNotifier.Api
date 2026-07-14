using HermesNotifier.Api.Data;
using HermesNotifier.Api.DTOs.Requests.Products;
using HermesNotifier.Api.DTOs.Responses.Products;
using HermesNotifier.Api.Infrastructure;
using HermesNotifier.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;

namespace HermesNotifier.Api.Controllers;

[Route("api/products")]
[ApiController]
public class ProductController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductController> _logger;
    private readonly IConfiguration _config;
    private readonly IOutputCacheStore _cacheStore;
    private const string HermesUrl = "https://www.hermes.com/tw/zh/";
    private const string StatusInStock = "InStock";
    private const string StatusOutOfStock = "OutOfStock";
    private const string StatusNotFound = "NotFound";

    public ProductController(
        ApplicationDbContext context,
        ILogger<ProductController> logger,
        IConfiguration config,
        IOutputCacheStore cacheStore)
    {
        _context = context;
        _logger = logger;
        _config = config;
        _cacheStore = cacheStore;
    }

    /// <summary>
    /// 取得所有產品清單 (帶快取)。
    /// onlyExpired=true 時只回傳「Cloudflare 快取已過期或從未抓取」的商品，
    /// 供爬蟲排程只重抓「重抓才可能拿到更新資料」的項目，省下重複拿快取副本的流量。
    /// </summary>
    /// <param name="onlyExpired">是否只回傳快取已過期（含從未抓取）的商品</param>
    /// <returns>產品清單</returns>
    [HttpGet]
    [OutputCache(PolicyName = "ProductsList")]
    public async Task<ActionResult<ProductListResponse>> GetAllProducts(
        [FromQuery] bool onlyExpired = false,
        [FromQuery] string? category = null)
    {
        try
        {
            var query = _context.Products.AsQueryable();

            // 依分類篩選（例：category=包款 / 小皮件）；未提供則全部
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(p => p.Category == category);
            }

            if (onlyExpired)
            {
                // 只選「快取已過期（CacheExpiresAt <= 現在）或從未抓取（null）」的商品，
                // 並讓最久未更新（含 null）排在最前面優先重抓。
                var now = TaiwanTime.Now;
                query = query
                    .Where(p => p.CacheExpiresAt == null || p.CacheExpiresAt <= now)
                    .OrderBy(p => p.CacheExpiresAt);
            }
            else
            {
                query = query.OrderByDescending(p => p.CreatedAt);
            }

            var products = await query.ToListAsync();

            var response = new ProductListResponse
            {
                TotalCount = products.Count,
                Products = products.Select(p => new ProductItemDto
                {
                    ProductId = p.ProductId,
                    ProductUrl = p.ProductUrl,
                    Title = p.Title,
                    Price = p.Price,
                    ImageUrl = p.ImageUrl,
                    Color = p.Color,
                    Category = p.Category,
                    IsAvailable = p.IsAvailable,
                    AvailabilityStatus = p.AvailabilityStatus,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    CacheExpiresAt = p.CacheExpiresAt
                }).ToList()
            };

            _logger.LogInformation("成功取得 {count} 個產品 (onlyExpired={onlyExpired}, category={category})", response.TotalCount, onlyExpired, category ?? "(全部)");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得產品清單時發生錯誤");
            return StatusCode(500, new ProductListResponse
            {
                TotalCount = 0,
                Products = new List<ProductItemDto>()
            });
        }
    }

    /// <summary>
    /// 根據 ProductId 取得單一產品
    /// </summary>
    /// <param name="productId">產品 ID</param>
    /// <returns>產品資訊</returns>
    [HttpGet("{productId}")]
    public async Task<ActionResult<ProductItemDto>> GetProductById(string productId)
    {
        try
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null)
            {
                _logger.LogWarning("找不到產品 ID: {productId}", productId);
                return NotFound(new { Message = $"找不到產品 ID: {productId}" });
            }

            var response = new ProductItemDto
            {
                ProductId = product.ProductId,
                ProductUrl = product.ProductUrl,
                Title = product.Title,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                Color = product.Color,
                Category = product.Category,
                IsAvailable = product.IsAvailable,
                AvailabilityStatus = product.AvailabilityStatus,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                CacheExpiresAt = product.CacheExpiresAt
            };

            _logger.LogInformation("成功取得產品: {productId}", productId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得產品時發生錯誤: {productId}", productId);
            return StatusCode(500, new { Message = $"取得產品失敗：{ex.Message}" });
        }
    }

    /// <summary>
    /// 更新產品資料
    /// </summary>
    /// <param name="productId">產品 ID</param>
    /// <param name="request">更新請求（部分欄位）</param>
    /// <returns>更新結果</returns>
    [HttpPatch("{productId}")]
    public async Task<ActionResult> UpdateProduct(
        string productId,
        [FromBody] UpdateProductRequest request)
    {
        try
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null)
            {
                _logger.LogWarning("找不到產品 ID: {productId}", productId);
                return NotFound(new { Message = $"找不到產品 ID: {productId}" });
            }

            var changed = false;

            if (!string.IsNullOrWhiteSpace(request.Title) && product.Title != request.Title)
            {
                product.Title = request.Title;
                changed = true;
            }

            if (request.Price.HasValue && product.Price != request.Price.Value)
            {
                product.Price = request.Price.Value;
                changed = true;
            }

            if (request.ImageUrl is not null && product.ImageUrl != request.ImageUrl)
            {
                product.ImageUrl = request.ImageUrl;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(request.ProductUrl) && product.ProductUrl != request.ProductUrl)
            {
                product.ProductUrl = request.ProductUrl;
                changed = true;
            }

            if (request.Color is not null && product.Color != request.Color)
            {
                product.Color = request.Color;
                changed = true;
            }

            if (request.IsAvailable.HasValue || !string.IsNullOrWhiteSpace(request.AvailabilityStatus))
            {
                if (!TryResolveAvailabilityState(
                        request.AvailabilityStatus,
                        request.IsAvailable,
                        out var resolvedStatus,
                        out var resolvedIsAvailable,
                        out var errorMessage))
                {
                    return BadRequest(new { Message = errorMessage });
                }

                if (product.AvailabilityStatus != resolvedStatus || product.IsAvailable != resolvedIsAvailable)
                {
                    product.AvailabilityStatus = resolvedStatus;
                    product.IsAvailable = resolvedIsAvailable;
                    changed = true;
                }
            }

            if (!changed)
            {
                return Ok(new
                {
                    Message = "沒有欄位異動",
                    ProductId = product.ProductId,
                    Changed = false
                });
            }

            product.UpdatedAt = TaiwanTime.Now;
            await _context.SaveChangesAsync();

            await _cacheStore.EvictByTagAsync("products-cache", default);

            return Ok(new
            {
                Message = "產品更新成功",
                ProductId = product.ProductId,
                Changed = true,
                UpdatedAt = product.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新產品資料失敗: {productId}", productId);
            return StatusCode(500, new { Message = $"更新失敗：{ex.Message}" });
        }
    }

    /// <summary>
    /// 更新產品的上架狀態
    /// </summary>
    /// <param name="productId">產品 ID</param>
    /// <param name="request">更新請求</param>
    /// <returns>更新結果</returns>
    [HttpPatch("{productId}/availability")]
    public async Task<ActionResult> UpdateProductAvailability(
        string productId, 
        [FromBody] UpdateProductAvailabilityRequest request)
    {
        try
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null)
            {
                _logger.LogWarning("找不到產品 ID: {productId}", productId);
                return NotFound(new { Message = $"找不到產品 ID: {productId}" });
            }

            if (!TryResolveAvailabilityState(
                    request.AvailabilityStatus,
                    request.IsAvailable,
                    out var resolvedStatus,
                    out var resolvedIsAvailable,
                    out var errorMessage))
            {
                return BadRequest(new { Message = errorMessage });
            }

            // 是否異動庫存狀態（相容舊 bool + 新三態）
            var statusChanged = product.AvailabilityStatus != resolvedStatus;
            var availabilityChanged = product.IsAvailable != resolvedIsAvailable;

            // 無論上架狀態是否改變，只要爬蟲回報了快取到期時間就更新
            // （供下一輪 GetAllProducts?onlyExpired=true 判斷此商品是否該重抓）。
            var incomingCacheExpiry = TaiwanTime.ToTaiwan(request.CacheExpiresAt);
            var cacheExpiryChanged = incomingCacheExpiry.HasValue
                && product.CacheExpiresAt != incomingCacheExpiry;
            if (cacheExpiryChanged)
            {
                product.CacheExpiresAt = incomingCacheExpiry;
            }

            if (!statusChanged && !availabilityChanged && !cacheExpiryChanged)
            {
                _logger.LogInformation("產品 {productId} 的庫存狀態與快取到期時間皆未改變，無需更新", productId);
                return Ok(new
                {
                    Message = "產品狀態未改變",
                    ProductId = productId,
                    IsAvailable = product.IsAvailable,
                    AvailabilityStatus = product.AvailabilityStatus,
                    Changed = false
                });
            }

            // 更新值
            var oldStatus = product.AvailabilityStatus;
            var oldValue = product.IsAvailable;
            if (statusChanged || availabilityChanged)
            {
                product.AvailabilityStatus = resolvedStatus;
                product.IsAvailable = resolvedIsAvailable;
            }
            product.UpdatedAt = TaiwanTime.Now;

            await _context.SaveChangesAsync();

            // 僅在狀態真的改變時才記錄狀態變更
            if (statusChanged || availabilityChanged)
            {
                var log = new ProductLog
                {
                    ProductId = product.Id,
                    Action = GetProductLogAction(resolvedStatus),
                    LoggedAt = TaiwanTime.Now
                };
                await _context.ProductLogs.AddAsync(log);
                await _context.SaveChangesAsync();
            }

            // 清除產品清單快取（含全清單與只取快取已過期清單）
            await _cacheStore.EvictByTagAsync("products-cache", default);
            _logger.LogInformation(
                "已更新產品 {productId} (statusChanged={statusChanged} {oldStatus}->{newStatus}, availabilityChanged={availabilityChanged} {oldValue}->{newValue}, cacheExpiresAt={cacheExpiresAt})，並清除快取",
                productId, statusChanged, oldStatus, resolvedStatus, availabilityChanged, oldValue, resolvedIsAvailable, product.CacheExpiresAt);

            // 補貨通知：僅在「狀態真的改變」且「變為可購買（缺貨/404→有貨）」時發送 LINE，
            // 讓爬蟲透過 /availability 更新庫存時也能推播補貨快訊（不影響更新 API 的成功結果）。
            if (statusChanged && resolvedStatus == StatusInStock)
            {
                try
                {
                    await BroadcastLineMessageAsync(new List<Product> { product });
                }
                catch (Exception notifyEx)
                {
                    _logger.LogError(notifyEx, "補貨通知發送失敗: {productId}", productId);
                }
            }

            return Ok(new
            {
                Message = "成功更新產品狀態",
                ProductId = productId,
                IsAvailable = product.IsAvailable,
                AvailabilityStatus = product.AvailabilityStatus,
                Changed = true,
                UpdatedAt = product.UpdatedAt,
                CacheExpiresAt = product.CacheExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新產品 {productId} 的上架狀態時發生錯誤", productId);
            return StatusCode(500, new { Message = $"更新失敗：{ex.Message}" });
        }
    }

    [HttpPost("sync")]
    public async Task<ActionResult<SyncProductsResponse>> SyncProducts([FromBody] SyncProductsRequest request)
    {
        try
        {
            var incomingProductIds = request.Products.Select(p => p.ProductId).ToHashSet();
            var existingProducts = await _context.Products.ToListAsync();
            var existingProductDict = existingProducts.ToDictionary(p => p.ProductId);

            var productsToNotify = new List<Product>();
            var productsToUpdate = new List<Product>();
            var productsToMarkUnavailable = new List<Product>();
            var logsToAdd = new List<ProductLog>();

            // 處理傳入的商品
            foreach (var dto in request.Products)
            {
                if (existingProductDict.TryGetValue(dto.ProductId, out var existingProduct))
                {
                    // 商品已存在
                    if (!existingProduct.IsAvailable)
                    {
                        // 商品重新上架
                        existingProduct.IsAvailable = true;
                        existingProduct.AvailabilityStatus = StatusInStock;
                        existingProduct.UpdatedAt = TaiwanTime.Now;
                        existingProduct.Title = dto.Title;
                        existingProduct.Price = dto.Price;

                        // 只有當傳入的 ImageUrl 不為空時才更新，否則保留原有的
                        if (!string.IsNullOrWhiteSpace(dto.ImageUrl))
                        {
                            existingProduct.ImageUrl = dto.ImageUrl;
                        }

                        existingProduct.ProductUrl = dto.ProductUrl;
                        existingProduct.Color = dto.Color;
                        if (!string.IsNullOrWhiteSpace(dto.Category))
                        {
                            existingProduct.Category = dto.Category;
                        }
                        productsToUpdate.Add(existingProduct);
                        productsToNotify.Add(existingProduct); // 重新上架也算新品通知
                    }
                    else
                    {
                        // 商品已上架，檢查是否有資訊需要更新
                        bool hasChanges = false;

                        if (existingProduct.Title != dto.Title)
                        {
                            existingProduct.Title = dto.Title;
                            hasChanges = true;
                        }

                        if (existingProduct.Price != dto.Price)
                        {
                            existingProduct.Price = dto.Price;
                            hasChanges = true;
                        }

                        if (!string.IsNullOrWhiteSpace(dto.ImageUrl) && existingProduct.ImageUrl != dto.ImageUrl)
                        {
                            existingProduct.ImageUrl = dto.ImageUrl;
                            hasChanges = true;
                        }

                        if (existingProduct.ProductUrl != dto.ProductUrl)
                        {
                            existingProduct.ProductUrl = dto.ProductUrl;
                            hasChanges = true;
                        }

                        if (existingProduct.Color != dto.Color)
                        {
                            existingProduct.Color = dto.Color;
                            hasChanges = true;
                        }

                        if (hasChanges)
                        {
                            existingProduct.UpdatedAt = TaiwanTime.Now;
                            productsToUpdate.Add(existingProduct);
                        }
                    }
                }
                else
                {
                    // 新商品
                    var newProduct = new Product
                    {
                        ProductId = dto.ProductId,
                        Title = dto.Title,
                        Price = dto.Price,
                        ImageUrl = dto.ImageUrl,
                        ProductUrl = dto.ProductUrl,
                        Color = dto.Color,
                        Category = string.IsNullOrWhiteSpace(dto.Category) ? "包款" : dto.Category,
                        IsAvailable = true,
                        AvailabilityStatus = StatusInStock
                    };
                    await _context.Products.AddAsync(newProduct);
                    productsToNotify.Add(newProduct);
                }
            }

            // 找出要標記為下架的商品（資料庫有且是上架狀態，但傳入的沒有）
            foreach (var existingProduct in existingProducts)
            {
                if (existingProduct.IsAvailable && !incomingProductIds.Contains(existingProduct.ProductId))
                {
                    existingProduct.IsAvailable = false;
                    existingProduct.AvailabilityStatus = StatusOutOfStock;
                    existingProduct.UpdatedAt = TaiwanTime.Now;
                    productsToMarkUnavailable.Add(existingProduct);
                }
            }

            if (productsToUpdate.Any())
            {
                _logger.LogInformation("準備更新 {count} 個商品（重新上架）", productsToUpdate.Count);
            }

            if (productsToMarkUnavailable.Any())
            {
                _logger.LogInformation("準備標記 {count} 個商品為下架", productsToMarkUnavailable.Count);
            }

            // 先儲存變更以取得新商品的 Id
            await _context.SaveChangesAsync();

            // 建立上架記錄（新商品或重新上架）
            foreach (var product in productsToNotify)
            {
                logsToAdd.Add(new ProductLog
                {
                    ProductId = product.Id,
                    Action = "Available",
                    LoggedAt = TaiwanTime.Now
                });
            }

            // 建立下架記錄
            foreach (var product in productsToMarkUnavailable)
            {
                logsToAdd.Add(new ProductLog
                {
                    ProductId = product.Id,
                    Action = "Unavailable",
                    LoggedAt = TaiwanTime.Now
                });
            }

            // 儲存 Log
            if (logsToAdd.Any())
            {
                await _context.ProductLogs.AddRangeAsync(logsToAdd);
                await _context.SaveChangesAsync();
                _logger.LogInformation("已記錄 {count} 筆商品狀態變更", logsToAdd.Count);
            }

            // 依需求：/sync 不發送 LINE 通知，通知只保留在 /{productId}/availability 且變為 InStock。
            var notifiedCount = 0;

            // 清除產品快取（因為資料已更新）
            await _cacheStore.EvictByTagAsync("products-cache", default);
            _logger.LogInformation("已清除產品快取");

            return Ok(new SyncProductsResponse
            {
                AddedCount = productsToNotify.Count,
                DeletedCount = productsToMarkUnavailable.Count,
                NotifiedCount = notifiedCount,
                Message = $"同步完成：新增/重新上架 {productsToNotify.Count} 個商品，下架 {productsToMarkUnavailable.Count} 個商品（/sync 不發通知）"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步商品時發生錯誤");
            return StatusCode(500, new SyncProductsResponse
            {
                Message = $"同步失敗：{ex.Message}"
            });
        }
    }

    /// <summary>
    /// Discovery：接收分類頁掃描到的 SKU，只「新增」資料庫沒有的商品（upsert）。
    /// 與 /sync 不同：不做全量對帳（不會把清單外商品標記下架）、不發送 LINE 通知。
    /// 新商品以 IsAvailable=false 入庫，之後由每輪庫存監控（/availability）抓到 InStock 時才發補貨通知。
    /// </summary>
    [HttpPost("discover")]
    public async Task<ActionResult> DiscoverProducts([FromBody] DiscoverProductsRequest request)
    {
        try
        {
            var existingDict = await _context.Products
                .ToDictionaryAsync(p => p.ProductId, p => p);
            var toAdd = new List<Product>();
            var updatedCount = 0;

            foreach (var dto in request.Products)
            {
                if (string.IsNullOrWhiteSpace(dto.ProductId))
                {
                    continue;
                }

                if (existingDict.TryGetValue(dto.ProductId, out var existing))
                {
                    var changed = false;

                    if (!string.IsNullOrWhiteSpace(dto.Title) && existing.Title != dto.Title)
                    {
                        existing.Title = dto.Title;
                        changed = true;
                    }

                    if (dto.Price > 0 && existing.Price != dto.Price)
                    {
                        existing.Price = dto.Price;
                        changed = true;
                    }

                    if (!string.IsNullOrWhiteSpace(dto.ImageUrl) && existing.ImageUrl != dto.ImageUrl)
                    {
                        existing.ImageUrl = dto.ImageUrl;
                        changed = true;
                    }

                    if (!string.IsNullOrWhiteSpace(dto.ProductUrl) && existing.ProductUrl != dto.ProductUrl)
                    {
                        existing.ProductUrl = dto.ProductUrl;
                        changed = true;
                    }

                    if (!string.IsNullOrWhiteSpace(dto.Color) && existing.Color != dto.Color)
                    {
                        existing.Color = dto.Color;
                        changed = true;
                    }

                    if (!string.IsNullOrWhiteSpace(dto.Category) && existing.Category != dto.Category)
                    {
                        existing.Category = dto.Category;
                        changed = true;
                    }

                    if (changed)
                    {
                        existing.UpdatedAt = TaiwanTime.Now;
                        updatedCount++;
                    }

                    continue;
                }

                var url = !string.IsNullOrWhiteSpace(dto.ProductUrl)
                    ? dto.ProductUrl
                    : $"https://www.hermes.com/tw/zh/product/{dto.ProductId}/";

                toAdd.Add(new Product
                {
                    ProductId = dto.ProductId,
                    Title = string.IsNullOrWhiteSpace(dto.Title) ? dto.ProductId : dto.Title,
                    Price = dto.Price,
                    ImageUrl = dto.ImageUrl,
                    ProductUrl = url,
                    Color = dto.Color,
                    Category = string.IsNullOrWhiteSpace(dto.Category) ? "包款" : dto.Category,
                    IsAvailable = false,   // 先設 false，等監控抓到 InStock 由 /availability 發補貨通知
                    AvailabilityStatus = StatusOutOfStock,
                    CacheExpiresAt = null  // null → 立即納入 onlyExpired，下一輪就檢查
                });
                existingDict[dto.ProductId] = toAdd[^1]; // 避免同批重複
            }

            if (toAdd.Any() || updatedCount > 0)
            {
                if (toAdd.Any())
                {
                    await _context.Products.AddRangeAsync(toAdd);
                }
                await _context.SaveChangesAsync();
                await _cacheStore.EvictByTagAsync("products-cache", default);
            }

            _logger.LogInformation("Discovery 完成：收到 {received} 個 SKU，新增 {added} 個新商品，更新 {updated} 個既有商品（不對帳、不通知）",
                request.Products.Count, toAdd.Count, updatedCount);

            return Ok(new
            {
                Message = $"Discovery 完成：新增 {toAdd.Count} 個新商品，更新 {updatedCount} 個既有商品",
                ReceivedCount = request.Products.Count,
                AddedCount = toAdd.Count,
                UpdatedCount = updatedCount,
                AddedSkus = toAdd.Select(p => p.ProductId).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discovery 處理時發生錯誤");
            return StatusCode(500, new { Message = $"Discovery 失敗：{ex.Message}" });
        }
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

    private static bool TryResolveAvailabilityState(
        string? availabilityStatus,
        bool? isAvailable,
        out string resolvedStatus,
        out bool resolvedIsAvailable,
        out string errorMessage)
    {
        resolvedStatus = StatusOutOfStock;
        resolvedIsAvailable = false;
        errorMessage = string.Empty;

        var hasStatus = !string.IsNullOrWhiteSpace(availabilityStatus);
        var hasBool = isAvailable.HasValue;

        if (!hasStatus && !hasBool)
        {
            errorMessage = "請至少提供 isAvailable 或 availabilityStatus。";
            return false;
        }

        var statusOk = true;
        var statusFromText = string.Empty;
        var boolFromText = false;

        if (hasStatus)
        {
            statusOk = TryNormalizeAvailabilityStatus(availabilityStatus!, out statusFromText, out boolFromText);
            if (!statusOk)
            {
                errorMessage = "availabilityStatus 僅支援 InStock/OutOfStock/NotFound（或 true/false/404）。";
                return false;
            }
        }

        if (hasStatus && hasBool && boolFromText != isAvailable!.Value)
        {
            errorMessage = "isAvailable 與 availabilityStatus 不一致。";
            return false;
        }

        if (hasStatus)
        {
            resolvedStatus = statusFromText;
            resolvedIsAvailable = boolFromText;
            return true;
        }

        resolvedStatus = isAvailable!.Value ? StatusInStock : StatusOutOfStock;
        resolvedIsAvailable = isAvailable.Value;
        return true;
    }

    private static bool TryNormalizeAvailabilityStatus(string input, out string status, out bool isAvailable)
    {
        status = StatusOutOfStock;
        isAvailable = false;

        var normalized = input.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "instock":
            case "in_stock":
            case "available":
            case "true":
                status = StatusInStock;
                isAvailable = true;
                return true;

            case "outofstock":
            case "out_of_stock":
            case "unavailable":
            case "false":
                status = StatusOutOfStock;
                isAvailable = false;
                return true;

            case "notfound":
            case "not_found":
            case "404":
                status = StatusNotFound;
                isAvailable = false;
                return true;

            default:
                return false;
        }
    }

    private async Task<int> BroadcastLineMessageAsync(List<Product> products)
    {
        var token = _config.GetLineChannelAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Line:ChannelAccessToken / LINE_BOT_CHANNEL_ACCESS_TOKEN 未設定，無法使用 LINE Messaging API 廣播。");
            return 0;
        }

        try
        {
            // 取得訂閱未過期的使用者
            var now = TaiwanTime.Now;
            var activeSubscribers = await _context.Users
                .Where(u => u.SubscribedUntil.HasValue && u.SubscribedUntil.Value > now)
                .ToListAsync();

            if (!activeSubscribers.Any())
            {
                _logger.LogInformation("沒有訂閱中的使用者，跳過通知發送");
                return 0;
            }

            _logger.LogInformation("找到 {count} 個訂閱中的使用者", activeSubscribers.Count);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var sentProductCount = 0;
            var productBatches = products.Chunk(12).ToArray();

            for (var batchIndex = 0; batchIndex < productBatches.Length; batchIndex++)
            {
                var batch = productBatches[batchIndex];
                var bubbles = batch.Select(p =>
                {
                    // 容錯處理：如果 ProductUrl 是空的，使用 Hermès 官網首頁
                    var lineTargetUrl = string.IsNullOrWhiteSpace(p.ProductUrl) 
                        ? HermesUrl 
                        : p.ProductUrl;

                    // 建立 bubble，如果有圖片就加上 hero
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

                    // body 區塊（必定存在）
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
                    altText = $"Hermès 新品上架 - 共 {products.Count} 件商品",
                    contents = new
                    {
                        type = "carousel",
                        contents = bubbles
                    }
                };

                // 使用 Multicast API 發送給指定的訂閱使用者
                var lineUserIds = activeSubscribers.Select(u => u.LineId).ToList();

                // LINE Multicast API 一次最多發送給 500 個使用者
                var userBatches = lineUserIds.Chunk(500).ToArray();

                foreach (var userBatch in userBatches)
                {
                    var payload = new
                    {
                        to = userBatch.ToArray(),
                        messages = new[] { flexMessage }
                    };

                    var response = await client.PostAsJsonAsync("https://api.line.me/v2/bot/message/multicast", payload);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        sentProductCount += batch.Length;
                        _logger.LogInformation(
                            "LINE multicast 成功 (batch={batchIndex}/{batchCount}, products={productCount}, users={userCount})：{body}",
                            batchIndex + 1,
                            productBatches.Length,
                            batch.Length,
                            userBatch.Count(),
                            responseBody);
                    }
                    else
                    {
                        _logger.LogError(
                            "LINE multicast 失敗 (batch={batchIndex}/{batchCount}, products={productCount}, users={userCount}, status={status})：{body}",
                            batchIndex + 1,
                            productBatches.Length,
                            batch.Length,
                            userBatch.Count(),
                            response.StatusCode,
                            responseBody);
                    }
                }
            }

            return sentProductCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LINE multicast 發送失敗");
            return 0;
        }
    }
}
