using System.Text.Json;
using System.Text.Json.Serialization;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Serialization;

namespace Paramore.Brighter.JsonConverters
{
    /// <summary>
    /// Global Configuration for the Json Serializer
    /// We provide custom type converters, particularly around serializing objects so that they appear as types not JsonElement
    /// The <see cref="JsonSerializerOptions.PropertyNamingPolicy"/> (camelCase) applies to C# property names only.
    /// Dictionary keys — such as <see cref="MessageHeader.Bag"/> keys — are serialized verbatim by
    /// <see cref="Serialization.DictionaryStringObjectJsonConverter"/>, so a key written as "SessionId" is read back
    /// as "SessionId".
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
            opts.Converters.Add(new BaggageConverter());
            opts.Converters.Add(new IdConverter());
            opts.Converters.Add(new PartitionKeyConverter());
            opts.Converters.Add(new TraceStateConverter());
            opts.Converters.Add(new TraceParentConverter());

            Options = opts;
        }
    }
}
