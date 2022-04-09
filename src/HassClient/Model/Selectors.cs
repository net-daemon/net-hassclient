using JoySoftware.HomeAssistant.Helpers.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace JoySoftware.HomeAssistant.Model
{
    public record ActionSelector
    {
    }

    public record AddonSelector
    {
    }

    public record AreaSelector
    {
        public DeviceSelector? Device { get; init; }

        public EntitySelector? Entity { get; init; }
    }

    public record BooleanSelector
    {
    }

    public record DeviceSelector
    {
        public string? Integration { get; init; }

        public string? Manufacturer { get; init; }

        public string? Model { get; init; }

        public EntitySelector? Entity { get; init; }
    }

    public record EntitySelector
    {
        public string? Integration { get; init; }

        public string? Domain { get; init; }

        public string? DeviceClass { get; init; }
    }

    public record NumberSelector
    {
        [Required]
        public double Min { get; init; }

        [Required]
        public double Max { get; init; }

        public float? Step { get; init; }

        public string? UnitOfMeasurement { get; init; }

        [JsonConverter(typeof(NullableEnumStringConverter<NumberSelectorMode?>))]
        public NumberSelectorMode? Mode { get; init; }
    }

    public enum NumberSelectorMode
    {
        Box,
        Slider
    }

    public record ObjectSelector
    {
    }

    public record TargetSelector
    {
        public AreaSelector? Area { get; init; }

        public DeviceSelector? Device { get; init; }

        public EntitySelector? Entity { get; init; }
    }

    public record TextSelector
    {
        public bool? Multiline { get; init; }
    }

    public record TimeSelector
    {
    }
}