using System;
using System.Text;

namespace Paramore.Brighter.Core.Tests.MessageBodyTests;

public class MessageBodyValueCachingTests
{
    [Test]
    public async Task When_accessing_Value_multiple_times_should_return_same_reference()
    {
        var jsonPayload = "{\"key\":\"value\"}";
        var body = new MessageBody(jsonPayload);

        var firstAccess = body.Value;
        var secondAccess = body.Value;

        await Assert.That(ReferenceEquals(firstAccess, secondAccess)).IsTrue();
    }

    [Test]
    public async Task When_accessing_Value_from_byte_array_should_cache_result()
    {
        var jsonPayload = "{\"key\":\"value\"}";
        var bytes = Encoding.UTF8.GetBytes(jsonPayload);
        var body = new MessageBody(bytes, characterEncoding: CharacterEncoding.UTF8);

        var firstAccess = body.Value;
        var secondAccess = body.Value;

        await Assert.That(firstAccess).IsEqualTo(jsonPayload);
        await Assert.That(ReferenceEquals(firstAccess, secondAccess)).IsTrue();
    }

    [Test]
    public async Task When_accessing_Value_with_ascii_encoding_should_cache_result()
    {
        var payload = "hello world";
        var body = new MessageBody(payload, characterEncoding: CharacterEncoding.ASCII);

        var firstAccess = body.Value;
        var secondAccess = body.Value;

        await Assert.That(firstAccess).IsEqualTo(payload);
        await Assert.That(ReferenceEquals(firstAccess, secondAccess)).IsTrue();
    }

    [Test]
    public async Task When_accessing_Value_with_base64_encoding_should_cache_result()
    {
        var originalBytes = Encoding.UTF8.GetBytes("hello world");
        var base64String = Convert.ToBase64String(originalBytes);
        var body = new MessageBody(base64String, characterEncoding: CharacterEncoding.Base64);

        var firstAccess = body.Value;
        var secondAccess = body.Value;

        await Assert.That(firstAccess).IsEqualTo(base64String);
        await Assert.That(ReferenceEquals(firstAccess, secondAccess)).IsTrue();
    }
}
