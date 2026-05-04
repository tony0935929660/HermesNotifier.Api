using System.ComponentModel.DataAnnotations;

namespace HermesNotifier.Api.DTOs.Requests.Products;

public class ProductDto
{
    [Required(ErrorMessage = "ProductId 不能為空")]
    public required string ProductId { get; set; }

    [Required(ErrorMessage = "Title 不能為空")]
    public required string Title { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Price 必須大於等於 0")]
    public decimal Price { get; set; }

    public string? ImageUrl { get; set; }

    [Required(ErrorMessage = "ProductUrl 不能為空")]
    [Url(ErrorMessage = "ProductUrl 必須是有效的 URL")]
    public required string ProductUrl { get; set; }

    public string? Color { get; set; }
}
