using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Model
{
    public record HassContext
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("parent_id")]
        public string? ParentId { get; set; }

        [JsonPropertyName("user_id")]
        public string? UserId { get; set; }
    }
}