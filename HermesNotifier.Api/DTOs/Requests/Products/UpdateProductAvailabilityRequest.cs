using System.ComponentModel.DataAnnotations;

namespace HermesNotifier.Api.DTOs.Requests.Products;

public class UpdateProductAvailabilityRequest
{
    [Required(ErrorMessage = "IsAvailable 不能為空")]
    public required bool IsAvailable { get; set; }

    /// <summary>
    /// 選填：爬蟲依回應 cache-control max-age 與 age 算出的「此商品頁 Cloudflare 快取到期時間 (UTC)」。
    /// 無論上架狀態是否改變都會寫入，供 GetAllProducts?onlyExpired=true 判斷下一輪是否該重抓。
    /// </summary>
    public DateTime? CacheExpiresAt { get; set; }
}
