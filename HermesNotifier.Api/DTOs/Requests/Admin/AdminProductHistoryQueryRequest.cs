namespace HermesNotifier.Api.DTOs.Requests.Admin;

public class AdminProductHistoryQueryRequest
{
    /// <summary>
    /// 商品 ID（Products 表的 ProductId）。
    /// </summary>
    public string? ProductId { get; set; }
}