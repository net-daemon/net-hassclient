using System.Collections.Generic;

namespace JoySoftware.HomeAssistant.Model
{
    public record HassService
    {
        public string? Service { get; init; }
        public string? Description { get; init; }
        public IReadOnlyCollection<HassServiceField>? Fields { get; init; }
    }
}