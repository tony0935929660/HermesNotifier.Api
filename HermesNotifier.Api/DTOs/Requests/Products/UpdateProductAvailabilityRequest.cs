using System.ComponentModel.DataAnnotations;

namespace HermesNotifier.Api.DTOs.Requests.Products;

public class UpdateProductAvailabilityRequest
{
    [Required(ErrorMessage = "IsAvailable 不能為空")]
    public required bool IsAvailable { get; set; }
}
