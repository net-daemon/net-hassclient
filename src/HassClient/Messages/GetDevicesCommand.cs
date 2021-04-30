namespace JoySoftware.HomeAssistant.Messages
{
    // {"type":"config/device_registry/list","id":25}
    public record GetDevicesCommand : CommandMessage
    {
        public GetDevicesCommand() => Type = "config/device_registry/list";
    }
}