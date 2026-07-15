using HermesNotifier.Api.Infrastructure;

namespace HermesNotifier.Api.Models;

public class User
{
    public int Id { get; set; }

    public required string LineId { get; set; }

    public bool IsAdmin { get; set; } = false;

    public string? Name { get; set; }

    public DateTime CreatedAt { get; set; } = TaiwanTime.Now;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime? SubscribedUntil { get; set; }

    public User()
    {
        SubscribedUntil = TaiwanTime.Now.AddDays(30);
    }
}
