namespace HermesNotifier.Api.DTOs.Requests.Products;

public class UpdateProductRequest
{
    public string? Title { get; set; }

    public decimal? Price { get; set; }

    public string? ImageUrl { get; set; }

    public string? ProductUrl { get; set; }

    public string? Color { get; set; }

    public bool? IsAvailable { get; set; }

    public string? AvailabilityStatus { get; set; }
}