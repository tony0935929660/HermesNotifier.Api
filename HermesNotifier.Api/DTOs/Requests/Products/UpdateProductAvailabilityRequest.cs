using System.ComponentModel.DataAnnotations;

namespace HermesNotifier.Api.DTOs.Requests.Products;

public class UpdateProductAvailabilityRequest
{
    /// <summary>
    /// 舊版相容欄位：true=InStock，false=OutOfStock。
    /// 若有提供 AvailabilityStatus，兩者必須一致。
    /// </summary>
    public bool? IsAvailable { get; set; }

    /// <summary>
    /// 新版三態欄位：InStock / OutOfStock / NotFound（接受 true/false/404 別名）。
    /// </summary>
    public string? AvailabilityStatus { get; set; }

    /// <summary>
    /// 選填：爬蟲依回應 cache-control max-age 與 age 算出的「此商品頁 Cloudflare 快取到期時間 (UTC)」。
    /// 無論上架狀態是否改變都會寫入，供 GetAllProducts?onlyExpired=true 判斷下一輪是否該重抓。
    /// </summary>
    public DateTime? CacheExpiresAt { get; set; }
}
