namespace HermesNotifier.Api.DTOs.Requests.Admin;

public class AdminLogQueryRequest
{
    /// <summary>
    /// 關鍵字（比對商品名稱、產品 ID）。
    /// </summary>
    public string? Keyword { get; set; }
}
