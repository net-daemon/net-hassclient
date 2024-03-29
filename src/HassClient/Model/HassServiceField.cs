﻿namespace JoySoftware.HomeAssistant.Model
{
    public record HassServiceField
    {
        public string? Field { get; init; }
        public string? Description { get; init; }
        public bool? Required { get; set; }
        public object? Example { get; init; }
        public object? Selector { get; init; }
    }
}