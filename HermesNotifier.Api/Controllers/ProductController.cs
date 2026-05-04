using HermesNotifier.Api.Data;
using HermesNotifier.Api.DTOs.Requests.Products;
using HermesNotifier.Api.DTOs.Responses.Products;
using HermesNotifier.Api.Models;
using Microsoft.AspNetCore.Mvc;
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
    private const string HermesUrl = "https://www.hermes.com/tw/zh/";

    public ProductController(
        ApplicationDbContext context,
        ILogger<ProductController> logger,
        IConfiguration config)
    {
        _context = context;
        _logger = logger;
        _config = config;
    }

    [HttpPost("sync")]
    public async Task<ActionResult<SyncProductsResponse>> SyncProducts([FromBody] SyncProductsRequest request)
    {
        try
        {
            var incomingProductIds = request.Products.Select(p => p.ProductId).ToHashSet();
            var existingProducts = await _context.Products.ToListAsync();
            var existingProductDict = existingProducts.ToDictionary(p => p.ProductId);

            var productsToAdd = new List<Product>();
            var productsToUpdate = new List<Product>();
            var productsToMarkUnavailable = new List<Product>();
            var logsToAdd = new List<ProductLog>();

            // 處理傳入的商品
            foreach (var dto in request.Products)
            {
                if (existingProductDict.TryGetValue(dto.ProductId, out var existingProduct))
                {
                    // 商品已存在，檢查是否需要更新狀態
                    if (!existingProduct.IsAvailable)
                    {
                        // 商品重新上架
                        existingProduct.IsAvailable = true;
                        existingProduct.UpdatedAt = DateTime.UtcNow;
                        existingProduct.Title = dto.Title;
                        existingProduct.Price = dto.Price;
                        existingProduct.ImageUrl = dto.ImageUrl;
                        existingProduct.ProductUrl = dto.ProductUrl;
                        existingProduct.Color = dto.Color;
                        productsToUpdate.Add(existingProduct);
                        productsToAdd.Add(existingProduct); // 重新上架也算新品通知
                    }
                    // 如果已經是上架狀態，不做任何處理
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
                    productsToAdd.Add(newProduct);
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

            // 建立上架記錄
            foreach (var product in productsToAdd)
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
            if (productsToAdd.Any())
            {
                notifiedCount = await BroadcastLineMessageAsync(productsToAdd);
            }

            return Ok(new SyncProductsResponse
            {
                AddedCount = productsToAdd.Count,
                DeletedCount = productsToMarkUnavailable.Count,
                NotifiedCount = notifiedCount,
                Message = $"同步完成：新增/重新上架 {productsToAdd.Count} 個商品，下架 {productsToMarkUnavailable.Count} 個商品，已通知 {notifiedCount} 個商品"
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
        var token = Environment.GetEnvironmentVariable("LINE_BOT_CHANNEL_ACCESS_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("環境變數 LINE_BOT_CHANNEL_ACCESS_TOKEN 未設定，無法使用 LINE Messaging API 廣播。");
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
                    var lineTargetUrl = p.ProductUrl;
                    return new
                    {
                        type = "bubble",
                        hero = new
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
                        },
                        body = new
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
                        }
                    };
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
