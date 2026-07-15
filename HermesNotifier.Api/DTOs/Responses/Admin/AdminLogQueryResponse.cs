namespace HermesNotifier.Api.DTOs.Responses.Admin;

public class AdminLogQueryResponse
{
    public int TotalCount { get; set; }

    public List<AdminLogItemDto> Items { get; set; } = new();
}

public class AdminLogItemDto
{
    public required string ProductId { get; set; }

    public required string Name { get; set; }

    public required string LatestStatus { get; set; }

    public DateTime LoggedAt { get; set; }
}
