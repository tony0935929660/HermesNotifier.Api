namespace HermesNotifier.Api.Models;

public class User
{
    public int Id { get; set; }

    public required string LineId { get; set; }

    public string? Name { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime? SubscribedUntil { get; set; }
}
