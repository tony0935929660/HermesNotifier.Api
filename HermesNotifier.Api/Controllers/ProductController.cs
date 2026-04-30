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
            var existingProductIds = existingProducts.Select(p => p.ProductId).ToHashSet();

            // 找出要刪除的商品（資料庫有但傳入的沒有）
            var productsToDelete = existingProducts
                .Where(p => !incomingProductIds.Contains(p.ProductId))
                .ToList();

            // 找出要新增的商品（傳入有但資料庫沒有）
            var productsToAdd = request.Products
                .Where(p => !existingProductIds.Contains(p.ProductId))
                .Select(dto => new Product
                {
                    ProductId = dto.ProductId,
                    Title = dto.Title,
                    Price = dto.Price,
                    ImageUrl = dto.ImageUrl,
                    ProductUrl = dto.ProductUrl,
                    Color = dto.Color
                })
                .ToList();

            // 刪除商品
            if (productsToDelete.Any())
            {
                _context.Products.RemoveRange(productsToDelete);
                _logger.LogInformation("準備刪除 {count} 個商品", productsToDelete.Count);
            }

            // 新增商品
            if (productsToAdd.Any())
            {
                await _context.Products.AddRangeAsync(productsToAdd);
                _logger.LogInformation("準備新增 {count} 個商品", productsToAdd.Count);
            }

            await _context.SaveChangesAsync();

            // 發送 LINE 通知（僅針對新增的商品）
            var notifiedCount = 0;
            if (productsToAdd.Any())
            {
                notifiedCount = await BroadcastLineMessageAsync(productsToAdd);
            }

            return Ok(new SyncProductsResponse
            {
                AddedCount = productsToAdd.Count,
                DeletedCount = productsToDelete.Count,
                NotifiedCount = notifiedCount,
                Message = $"同步完成：新增 {productsToAdd.Count} 個商品，刪除 {productsToDelete.Count} 個商品，已通知 {notifiedCount} 個新商品"
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
