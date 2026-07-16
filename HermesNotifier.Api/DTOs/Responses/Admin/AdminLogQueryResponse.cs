namespace HermesNotifier.Api.DTOs.Responses.Admin;

public class AdminLogQueryResponse
{
    public int TotalCount { get; set; }

    public List<AdminLogItemDto> Items { get; set; } = new();
}

public class AdminProductHistoryQueryResponse
{
    public required string ProductId { get; set; }

    public required string ProductName { get; set; }

    public int TotalCount { get; set; }

    public List<AdminProductHistoryItemDto> Items { get; set; } = new();
}

public class AdminProductHistoryItemDto
{
    public required string Action { get; set; }

    public required string Status { get; set; }

    public DateTime LoggedAt { get; set; }
}

public class AdminUserQueryResponse
{
    public int TotalCount { get; set; }

    public List<AdminUserItemDto> Items { get; set; } = new();
}

public class AdminUserItemDto
{
    public required string LineId { get; set; }

    public string? Name { get; set; }

    public bool IsAdmin { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime? SubscribedUntil { get; set; }
}

public class AdminLogItemDto
{
    public required string ProductId { get; set; }

    public required string Name { get; set; }

    public required string LatestStatus { get; set; }

    public DateTime LoggedAt { get; set; }
}
