namespace JoySoftware.HomeAssistant.Messages
{
    public record HassPingCommand : CommandMessage
    {
        public HassPingCommand() => Type = "ping";
    }
}