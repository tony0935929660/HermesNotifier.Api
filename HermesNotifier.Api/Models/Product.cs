namespace HermesNotifier.Api.Models;

public class Product
{
    public int Id { get; set; }

    public required string ProductId { get; set; }

    public required string Title { get; set; }

    public decimal Price { get; set; }

    public string? ImageUrl { get; set; }

    public required string ProductUrl { get; set; }

    public string? Color { get; set; }

    /// <summary>
    /// 商品分類：「包款」或「小皮件」。既有資料預設「包款」。
    /// 由 discovery 掃描分類頁時標記，供前端分群顯示與篩選。
    /// </summary>
    public string Category { get; set; } = "包款";

    public bool IsAvailable { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 此商品頁 Cloudflare 邊緣快取預計到期時間 (UTC)。
    /// 由爬蟲依回應 cache-control 的 max-age 與 age 標頭計算後回寫：
    /// expiresAt = now + (max-age - age)。
    /// null = 尚未抓取或未知，視為應立即重抓；
    /// 早於現在 = 快取已過期，重抓才可能取得更新後的資料。
    /// </summary>
    public DateTime? CacheExpiresAt { get; set; }
}
