using System.Text.Json;
using System.Text.Json.Serialization;
using Paramore.Brighter.Serialization;

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
            opts.Converters.Add(new DictionaryStringObjectJsonConverter());
            opts.Converters.Add(new ObjectToInferredTypesConverter());
            opts.Converters.Add(new JsonStringEnumConverter());

            Options = opts;
        }
    }
}
