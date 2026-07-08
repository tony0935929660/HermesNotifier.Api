namespace HermesNotifier.Api.Models;

public class ProductLog
{
    public int Id { get; set; }

    /// <summary>
    /// 關聯到 Products 表的 Id
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// 操作類型：Available (上架) / Unavailable (缺貨) / NotFound (404 下架)
    /// </summary>
    public required string Action { get; set; }

    /// <summary>
    /// 記錄時間
    /// </summary>
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 導航屬性
    /// </summary>
    public Product? Product { get; set; }
}
