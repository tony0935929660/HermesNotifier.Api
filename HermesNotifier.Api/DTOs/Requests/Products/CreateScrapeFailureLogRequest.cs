using System.ComponentModel.DataAnnotations;

namespace HermesNotifier.Api.DTOs.Requests.Products;

public class CreateScrapeFailureLogRequest
{
    [Required]
    [MaxLength(20)]
    public required string FailureType { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Verdict { get; set; }

    [Range(100, 599)]
    public int? HttpStatus { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Tier { get; set; }

    [Range(1, int.MaxValue)]
    public int Attempts { get; set; }

    [Range(0, int.MaxValue)]
    public int ElapsedMs { get; set; }

    public bool IsPendingInitialScrape { get; set; }
}