using System;
using System.Text;

namespace Paramore.Brighter.Core.Tests.MessageBodyTests;

public class MessageBodyReadOnlyMemoryConstructionTests
{
    [Test]
    public async Task When_constructing_from_ReadOnlyMemory_should_not_copy()
    {
        var jsonPayload = "{\"key\":\"value\"}";
        var bytes = Encoding.UTF8.GetBytes(jsonPayload);
        var memory = new ReadOnlyMemory<byte>(bytes);

        var body = new MessageBody(memory);

        await Assert.That(body.Memory.Length).IsEqualTo(memory.Length);
        await Assert.That(memory.Span.SequenceEqual(body.Memory.Span)).IsTrue();
        await Assert.That(memory.Equals(body.Memory)).IsTrue();
    }

    [Test]
    public async Task When_constructing_from_ReadOnlyMemory_should_expose_bytes_for_backward_compat()
    {
        var jsonPayload = "{\"key\":\"value\"}";
        var bytes = Encoding.UTF8.GetBytes(jsonPayload);
        var memory = new ReadOnlyMemory<byte>(bytes);

        var body = new MessageBody(memory);

        await Assert.That(body.Bytes.Length).IsEqualTo(bytes.Length);
        await Assert.That(body.Bytes).IsEquivalentTo(bytes);
    }

    [Test]
    public async Task When_constructing_from_ReadOnlyMemory_should_return_correct_value_string()
    {
        var jsonPayload = "{\"key\":\"value\"}";
        var bytes = Encoding.UTF8.GetBytes(jsonPayload);
        var memory = new ReadOnlyMemory<byte>(bytes);

        var body = new MessageBody(memory, characterEncoding: CharacterEncoding.UTF8);

        await Assert.That(body.Value).IsEqualTo(jsonPayload);
    }
}
