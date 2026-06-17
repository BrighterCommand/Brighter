using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

// Regression pin for issue #4149 (symptom 1) — prior to the fix, the
// [Newtonsoft.Json.JsonConverter] attributes on MessageHeader.Topic, ReplyTo, TraceParent
// and TraceState pointed at the System.Text.Json converter types, causing Newtonsoft.Json
// to throw InvalidCastException when building the contract for MessageHeader. Newtonsoft
// resolves the contract for the whole type up front, so a single mis-targeted attribute
// breaks serialisation of *any* property on the type — meaning all four attributes must
// be fixed together. This pin only covers serialisation + on-the-wire shape; full
// Newtonsoft round-trip of a MessageHeader also requires the Baggage Newtonsoft converter
// (symptom 2) which is covered by a separate test.
public class MessageHeaderNewtonsoftRoundTripTests
{
    [Fact]
    public void When_Serializing_A_Message_Header_Through_Newtonsoft_Custom_Converter_Properties_Are_Bare_Strings()
    {
        var header = new MessageHeader(
            messageId: "id-1",
            topic: new RoutingKey("the.topic"),
            messageType: MessageType.MT_EVENT,
            replyTo: new RoutingKey("the.reply.to"),
            traceParent: new TraceParent("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"),
            traceState: new TraceState("vendor=value"));

        var json = JsonConvert.SerializeObject(header);
        var parsed = JObject.Parse(json);

        Assert.Equal(JTokenType.String, parsed["Topic"]!.Type);
        Assert.Equal("the.topic", parsed["Topic"]!.Value<string>());

        Assert.Equal(JTokenType.String, parsed["ReplyTo"]!.Type);
        Assert.Equal("the.reply.to", parsed["ReplyTo"]!.Value<string>());

        Assert.Equal(JTokenType.String, parsed["TraceParent"]!.Type);
        Assert.Equal("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", parsed["TraceParent"]!.Value<string>());

        Assert.Equal(JTokenType.String, parsed["TraceState"]!.Type);
        Assert.Equal("vendor=value", parsed["TraceState"]!.Value<string>());
    }
}
