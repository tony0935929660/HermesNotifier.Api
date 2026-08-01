using HermesNotifier.Api.Infrastructure;

namespace HermesNotifier.Api.Models;

public class ScrapeFailureLog
{
    public long Id { get; set; }

    public int ProductId { get; set; }

    public required string FailureType { get; set; }

    public required string Verdict { get; set; }

    public int? HttpStatus { get; set; }

    public required string Tier { get; set; }

    public int Attempts { get; set; }

    public int ElapsedMs { get; set; }

    public bool IsPendingInitialScrape { get; set; }

    public DateTime LoggedAt { get; set; } = TaiwanTime.Now;

    public Product? Product { get; set; }
}