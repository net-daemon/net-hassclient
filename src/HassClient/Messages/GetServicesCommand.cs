namespace JoySoftware.HomeAssistant.Messages
{
    public record GetServicesCommand : CommandMessage
    {
        public GetServicesCommand() => Type = "get_services";
    }
}