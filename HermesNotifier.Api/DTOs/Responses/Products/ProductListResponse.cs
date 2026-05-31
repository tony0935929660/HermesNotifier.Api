namespace HermesNotifier.Api.DTOs.Responses.Products;

public class ProductListResponse
{
    public int TotalCount { get; set; }
    public List<ProductItemDto> Products { get; set; } = new();
}

public class ProductItemDto
{
    public required string ProductId { get; set; }
    public required string ProductUrl { get; set; }
    public required string Title { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Color { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
