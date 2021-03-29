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
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                AllowTrailingCommas = true
            };

            opts.Converters.Add(new JsonStringConverter());

            Options = opts;
        }
    }
}
