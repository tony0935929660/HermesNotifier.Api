namespace HermesNotifier.Api.DTOs.Requests.Admin;

public class AdminProductQueryRequest
{
    /// <summary>
    /// 分類：包款 / 小皮件。
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 關鍵字（比對商品名稱、產品 ID）。
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// 庫存狀態：InStock / OutOfStock / NotFound。
    /// </summary>
    public string? Status { get; set; }
}
