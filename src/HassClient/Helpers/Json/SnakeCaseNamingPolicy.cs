using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace JoySoftware.HomeAssistant.Helpers.Json
{
    public class SnakeCaseNamingPolicy : JsonNamingPolicy
    {
        public static SnakeCaseNamingPolicy Instance { get; } = new ();

        public override string ConvertName(string name)
        {
            return ToSnakeCase(name);
        }

        private static string ToSnakeCase(string str)
        {
            return string.Concat(str.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x : x.ToString())).ToLower(CultureInfo.InvariantCulture);
        }
    }
}