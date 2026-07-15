namespace HermesNotifier.Api.DTOs.Responses.Admin;

public class AdminProductQueryResponse
{
    public int TotalCount { get; set; }

    public List<AdminProductItemDto> Items { get; set; } = new();
}

public class AdminProductItemDto
{
    public required string ProductId { get; set; }

    public required string Name { get; set; }

    public decimal Price { get; set; }

    public string? Color { get; set; }

    public required string ProductUrl { get; set; }

    public required string Type { get; set; }

    public required string Status { get; set; }
}
