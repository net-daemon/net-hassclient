namespace JoySoftware.HomeAssistant.Model
{
    public enum EventType
    {
        All = 0,
        HomeAssistantStart = 1,
        HomeAssistantStop = 2,
        StateChanged = 3,
        TimeChanged = 4,
        ServiceRegistered = 5,
        CallService = 6,
        ServiceExecuted = 7,
        PlatformDiscovered = 8,
        ComponentLoaded = 9
    }
}