namespace JoySoftware.HomeAssistant.Messages
{
    // {"type":"config/area_registry/list","id":24}
    public record GetAreasCommand : CommandMessage
    {
        public GetAreasCommand() => Type = "config/area_registry/list";
    }
}