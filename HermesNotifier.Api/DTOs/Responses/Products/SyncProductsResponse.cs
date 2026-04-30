namespace HermesNotifier.Api.DTOs.Responses.Products;

public class SyncProductsResponse
{
    public int AddedCount { get; set; }

    public int DeletedCount { get; set; }

    public int NotifiedCount { get; set; }

    public string Message { get; set; } = string.Empty;
}
