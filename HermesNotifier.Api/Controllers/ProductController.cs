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
                    var lineTargetUrl = HermesUrl;
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

                var payload = new
                {
                    messages = new[]
                    {
                        new
                        {
                            type = "flex",
                            altText = $"Hermes 皮件商品通知 ({batchIndex + 1}/{productBatches.Length}) - 本批 {batch.Length} 個商品",
                            contents = new
                            {
                                type = "carousel",
                                contents = bubbles
                            }
                        }
                    }
                };

                var response = await client.PostAsJsonAsync("https://api.line.me/v2/bot/message/broadcast", payload);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    sentProductCount += batch.Length;
                    _logger.LogInformation(
                        "LINE broadcast 成功 (batch={batchIndex}/{batchCount}, size={batchSize})：{body}",
                        batchIndex + 1,
                        productBatches.Length,
                        batch.Length,
                        responseBody);
                }
                else
                {
                    _logger.LogError(
                        "LINE broadcast 失敗 (batch={batchIndex}/{batchCount}, size={batchSize}, status={status})：{body}",
                        batchIndex + 1,
                        productBatches.Length,
                        batch.Length,
                        response.StatusCode,
                        responseBody);
                }
            }

            return sentProductCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LINE broadcast 發送失敗");
            return 0;
        }
    }
}
