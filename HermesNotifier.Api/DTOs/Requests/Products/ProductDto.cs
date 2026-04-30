namespace HermesNotifier.Api.DTOs.Requests.Products;

public class ProductDto
{
    public required string ProductId { get; set; }

    public required string Title { get; set; }

    public decimal Price { get; set; }

    public required string ImageUrl { get; set; }

    public required string ProductUrl { get; set; }

    public string? Color { get; set; }
}
