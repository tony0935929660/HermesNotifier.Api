namespace HermesNotifier.Api.DTOs.Requests.Products;

public class SyncProductsRequest
{
    public required List<ProductDto> Products { get; set; }
}
