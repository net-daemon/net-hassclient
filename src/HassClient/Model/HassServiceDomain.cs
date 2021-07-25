using System.Collections.Generic;

namespace JoySoftware.HomeAssistant.Model
{
    public record HassServiceDomain
    {
        public string? Domain { get; init; }
        public IEnumerable<HassService>? Services { get; init; }
    }
}