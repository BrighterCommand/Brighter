using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Paramore.Brighter.JsonConverters;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

// Pins value-level parity for the four custom-converter properties on MessageHeader
// (Topic, ReplyTo, TraceParent, TraceState). Property *names* differ across the two
// stacks because Brighter's STJ options use camelCase while Newtonsoft default is
// PascalCase, but both serializers tolerate either casing on read, so cross-stack
// interop depends on the *values* matching, not byte-equal JSON. Full deserialisation
// parity for the whole MessageHeader is blocked on the unrelated ContentType issue
// (tracked separately) — this test stays on serialisation only.
public class MessageHeaderCustomPropertyParityTests
{
    [Test]
    public async Task When_Message_Header_Is_Serialised_The_Custom_Properties_Are_Identical_On_Both_Stacks()
    {
        var header = new MessageHeader(
            messageId: "id-1",
            topic: new RoutingKey("the.topic"),
            messageType: MessageType.MT_EVENT,
            replyTo: new RoutingKey("the.reply.to"),
            traceParent: new TraceParent("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"),
            traceState: new TraceState("vendor=value"));

        var stjJson = System.Text.Json.JsonSerializer.Serialize(header, JsonSerialisationOptions.Options);
        var newtonsoftJson = JsonConvert.SerializeObject(header);

        var stj = JObject.Parse(stjJson);
        var newtonsoft = JObject.Parse(newtonsoftJson);

        // Property *names* differ across stacks (STJ uses camelCase per
        // JsonSerialisationOptions; Newtonsoft default is PascalCase). Both serializers
        // tolerate either casing on read, so what matters for cross-stack interop is that
        // the property *values* (the bare strings emitted by the four custom converters)
        // are identical. That's what this pin asserts.
        foreach (var (stjName, newtonsoftName) in new[]
                 {
                     ("topic", "Topic"),
                     ("replyTo", "ReplyTo"),
                     ("traceParent", "TraceParent"),
                     ("traceState", "TraceState")
                 })
        {
            var stjToken = stj[stjName];
            var newtonsoftToken = newtonsoft[newtonsoftName];
            await Assert.That(stjToken).IsNotNull();
            await Assert.That(newtonsoftToken).IsNotNull();
            await Assert.That(stjToken!.Type).IsEqualTo(JTokenType.String);
            await Assert.That(newtonsoftToken!.Type).IsEqualTo(JTokenType.String);
            await Assert.That(newtonsoftToken.Value<string>()).IsEqualTo(stjToken.Value<string>());
        }
    }
}