using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paramore.Brighter
{
    /// <summary>
    /// Global Configuration for the Json Serialiser
    /// </summary>
    public static class JsonSerialisationOptions
    {
        public static JsonSerializerOptions Options { get; }

        static JsonSerialisationOptions()
        {
            Options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                AllowTrailingCommas = true
            };
        }
    }
}
