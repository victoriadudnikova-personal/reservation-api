using System.Text.Json;

namespace ReservationWebAPI.Application.Helpers
{
    public static class JsonHelper
    {
        public static JsonSerializerOptions DefaultOptions = new()
        {
            PropertyNamingPolicy = null,
            WriteIndented = false,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
            IncludeFields = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
            AllowTrailingCommas = false,
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        public static T? DeserializeJsonToObject<T>(string? json, JsonSerializerOptions? options = null)
        {
            if (json == null)
            {
                throw new ArgumentNullException("Deserialization string is null.");
            }
            return JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
        }

        public static string SerializeObjectToJson<T>(T? obj, JsonSerializerOptions? options = null)
        {
            if (obj == null)
            {
                throw new ArgumentNullException("Serialization object is null.");
            }
            return JsonSerializer.Serialize<T>(obj, options ?? DefaultOptions);
        }
    }
}
