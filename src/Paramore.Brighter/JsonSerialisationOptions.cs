using System.Text.Json;
using System.Text.Json.Serialization;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Serialization;

namespace Paramore.Brighter
{
    /// <summary>
    /// Global Configuration for the Json Serializer
    /// We provide custom type converters, particularly around serializing objects so that they appear as types not JsonElement
    /// The camelCase property will convert a key, such as a header bag key, to camelCase if you use UpperCase and so if you expect
    /// an UpperCase property you will find that it is now a camelCase one. It is best to use camelCase names for this reason
    /// </summary>
    public static class JsonSerialisationOptions
    {
        /// <summary>
        /// Brighter serialization options for use when serializing or deserializing text
        /// </summary>
        public static JsonSerializerOptions Options { get; set; }

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
            opts.Converters.Add(new SubscriptionNameConverter());
            opts.Converters.Add(new RoutingKeyConvertor());
            opts.Converters.Add(new ChannelNameConverter());
            opts.Converters.Add(new TraceStateConverter());

            Options = opts;
        }
    }
}
