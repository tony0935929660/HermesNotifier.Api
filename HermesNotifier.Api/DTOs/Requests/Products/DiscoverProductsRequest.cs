using System.ComponentModel.DataAnnotations;

namespace HermesNotifier.Api.DTOs.Requests.Products;

/// <summary>
/// Discovery（發現新品）請求。與 /sync 不同：只做 upsert（新增缺少的 SKU），
/// 不做全量對帳、不把清單外的商品標記下架、不發送 LINE 通知。
/// 用於分類頁掃描找出新上架 SKU 並加入監控清單。
/// </summary>
public class DiscoverProductsRequest
{
    [Required]
    public required List<DiscoverProductItem> Products { get; set; }
}

public class DiscoverProductItem
{
    [Required(ErrorMessage = "ProductId 不能為空")]
    public required string ProductId { get; set; }

    /// <summary>商品頁 URL；未提供時後端以 SKU 組出 /tw/zh/product/{SKU}/。</summary>
    public string? ProductUrl { get; set; }

    /// <summary>標題；未提供時以 SKU 代替。</summary>
    public string? Title { get; set; }

    public decimal Price { get; set; }

    public string? ImageUrl { get; set; }

    public string? Color { get; set; }

    /// <summary>「包款」或「小皮件」。未提供預設「包款」。</summary>
    public string? Category { get; set; }
}
