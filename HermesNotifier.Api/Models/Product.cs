namespace HermesNotifier.Api.Models;

public class Product
{
    public int Id { get; set; }

    public required string ProductId { get; set; }

    public required string Title { get; set; }

    public decimal Price { get; set; }

    public required string ImageUrl { get; set; }

    public required string ProductUrl { get; set; }

    public string? Color { get; set; }

    public bool IsAvailable { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
