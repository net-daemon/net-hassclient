namespace JoySoftware.HomeAssistant.Messages
{
    public record GetConfigCommand : CommandMessage
    {
        public GetConfigCommand() => Type = "get_config";
    }
}