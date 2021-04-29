namespace JoySoftware.HomeAssistant.Messages
{
    public record GetStatesCommand : CommandMessage
    {
        public GetStatesCommand() => Type = "get_states";
    }
}