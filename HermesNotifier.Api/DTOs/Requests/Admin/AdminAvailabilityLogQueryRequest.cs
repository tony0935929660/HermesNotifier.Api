namespace HermesNotifier.Api.DTOs.Requests.Admin;

public class AdminAvailabilityLogQueryRequest
{
    /// <summary>
    /// 關鍵字（比對商品名稱、產品 ID）。
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// 分頁頁碼，從 1 開始。
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// 每頁筆數。
    /// </summary>
    public int PageSize { get; set; } = 10;
}