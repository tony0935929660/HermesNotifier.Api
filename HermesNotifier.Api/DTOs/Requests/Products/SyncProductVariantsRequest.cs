using System.ComponentModel.DataAnnotations;

namespace HermesNotifier.Api.DTOs.Requests.Products;

public class SyncProductVariantsRequest
{
    [Required]
    public required string SourceProductId { get; set; }

    public DateTime? CacheExpiresAt { get; set; }

    [Required]
    public required List<SyncProductVariantItem> Variants { get; set; }
}

public class SyncProductVariantItem
{
    [Required]
    public required string ProductId { get; set; }

    [Required]
    [MaxLength(500)]
    public required string ProductUrl { get; set; }

    public bool IsAvailable { get; set; }
}