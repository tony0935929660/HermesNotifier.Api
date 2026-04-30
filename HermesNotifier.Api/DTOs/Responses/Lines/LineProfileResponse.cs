using System.Text.Json.Serialization;

namespace HermesNotifier.Api.DTOs.Responses.Lines
{
    public class LineProfileResponse
    {
        [JsonPropertyName("userId")]
        public required string UserId { get; set; }

        [JsonPropertyName("displayName")]
        public required string DisplayName { get; set; }

        [JsonPropertyName("pictureUrl")]
        public string? PictureUrl { get; set; }

        [JsonPropertyName("statusMessage")]
        public string? StatusMessage { get; set; }
    }
}
