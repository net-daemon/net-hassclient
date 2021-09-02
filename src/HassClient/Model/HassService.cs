using System.Collections.Generic;

namespace JoySoftware.HomeAssistant.Model
{
    public record HassService
    {
        public string? Service { get; init; }
        public string? Description { get; init; }
        public bool? Required { get; set; }
        public IReadOnlyCollection<HassServiceField>? Fields { get; init; }
        public TargetSelector? Target { get; set; }
    }
}