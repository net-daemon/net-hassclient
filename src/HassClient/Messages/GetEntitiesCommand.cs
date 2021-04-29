namespace JoySoftware.HomeAssistant.Messages
{
    // {"type":"config/entity_registry/list","id":29}
    public record GetEntitiesCommand : CommandMessage
    {
        public GetEntitiesCommand() => Type = "config/entity_registry/list";
    }
}