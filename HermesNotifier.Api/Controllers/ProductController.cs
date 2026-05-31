using HermesNotifier.Api.Data;
using HermesNotifier.Api.DTOs.Requests.Products;
using HermesNotifier.Api.DTOs.Responses.Products;
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
    /// 取得所有產品清單 (帶快取)
    /// </summary>
    /// <returns>產品清單</returns>
    [HttpGet]
    [OutputCache(PolicyName = "ProductsList")]
    public async Task<ActionResult<ProductListResponse>> GetAllProducts()
    {
        try
        {
            var products = await _context.Products
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

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
                    IsAvailable = p.IsAvailable,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                }).ToList()
            };

            _logger.LogInformation("成功取得 {count} 個產品", response.TotalCount);
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
                IsAvailable = product.IsAvailable,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
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

            // 檢查值是否有改變
            if (product.IsAvailable == request.IsAvailable)
            {
                _logger.LogInformation("產品 {productId} 的 IsAvailable 值未改變，無需更新", productId);
                return Ok(new 
                { 
                    Message = "產品狀態未改變",
                    ProductId = productId,
                    IsAvailable = product.IsAvailable,
                    Changed = false
                });
            }

            // 更新值
            var oldValue = product.IsAvailable;
            product.IsAvailable = request.IsAvailable;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // 記錄狀態變更
            var log = new ProductLog
            {
                ProductId = product.Id,
                Action = request.IsAvailable ? "Available" : "Unavailable",
                LoggedAt = DateTime.UtcNow
            };
            await _context.ProductLogs.AddAsync(log);
            await _context.SaveChangesAsync();

            // 清除產品清單快取
            await _cacheStore.EvictByTagAsync("products-cache", default);
            _logger.LogInformation(
                "已更新產品 {productId} 的 IsAvailable: {oldValue} -> {newValue}，並清除快取", 
                productId, oldValue, request.IsAvailable);

            return Ok(new 
            { 
                Message = "成功更新產品上架狀態",
                ProductId = productId,
                IsAvailable = product.IsAvailable,
                Changed = true,
                UpdatedAt = product.UpdatedAt
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
                        existingProduct.UpdatedAt = DateTime.UtcNow;
                        existingProduct.Title = dto.Title;
                        existingProduct.Price = dto.Price;

                        // 只有當傳入的 ImageUrl 不為空時才更新，否則保留原有的
                        if (!string.IsNullOrWhiteSpace(dto.ImageUrl))
                        {
                            existingProduct.ImageUrl = dto.ImageUrl;
                        }

                        existingProduct.ProductUrl = dto.ProductUrl;
                        existingProduct.Color = dto.Color;
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
                            existingProduct.UpdatedAt = DateTime.UtcNow;
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
                        IsAvailable = true
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
                    existingProduct.UpdatedAt = DateTime.UtcNow;
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
                    LoggedAt = DateTime.UtcNow
                });
            }

            // 建立下架記錄
            foreach (var product in productsToMarkUnavailable)
            {
                logsToAdd.Add(new ProductLog
                {
                    ProductId = product.Id,
                    Action = "Unavailable",
                    LoggedAt = DateTime.UtcNow
                });
            }

            // 儲存 Log
            if (logsToAdd.Any())
            {
                await _context.ProductLogs.AddRangeAsync(logsToAdd);
                await _context.SaveChangesAsync();
                _logger.LogInformation("已記錄 {count} 筆商品狀態變更", logsToAdd.Count);
            }

            // 發送 LINE 通知（僅針對新增或重新上架的商品）
            var notifiedCount = 0;
            if (productsToNotify.Any())
            {
                notifiedCount = await BroadcastLineMessageAsync(productsToNotify);
            }

            // 清除產品快取（因為資料已更新）
            await _cacheStore.EvictByTagAsync("products-cache", default);
            _logger.LogInformation("已清除產品快取");

            return Ok(new SyncProductsResponse
            {
                AddedCount = productsToNotify.Count,
                DeletedCount = productsToMarkUnavailable.Count,
                NotifiedCount = notifiedCount,
                Message = $"同步完成：新增/重新上架 {productsToNotify.Count} 個商品，下架 {productsToMarkUnavailable.Count} 個商品，已通知 {notifiedCount} 個商品"
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

    private async Task<int> BroadcastLineMessageAsync(List<Product> products)
    {
        var token = _config["Line:ChannelAccessToken"];
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Line:ChannelAccessToken 未設定，無法使用 LINE Messaging API 廣播。");
            return 0;
        }

        try
        {
            // 取得訂閱未過期的使用者
            var now = DateTime.UtcNow;
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
