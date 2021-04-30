namespace JoySoftware.HomeAssistant.Model
{
    public record HassServiceField
    {
        public string? Field { get; init; }
        
        public string? Description { get; init; }
        
        public object? Example { get; init; }
    }
}