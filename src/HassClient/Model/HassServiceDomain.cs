using System.Collections.Generic;

namespace JoySoftware.HomeAssistant.Model
{
    public record HassServiceDomain
    {
        public string? Domain { get; init; }
        public IReadOnlyCollection<HassService>? Services { get; init; }
    }
}