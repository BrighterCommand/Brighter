using System;
using System.Text;

namespace Paramore.Brighter.Core.Tests.MessageBodyTests;

public class MessageBodyEqualityTests
{
    [Test]
    public async Task When_two_bodies_have_identical_bytes_should_be_equal()
    {
        var payload = "{\"key\":\"value\"}";
        var body1 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payload)));
        var body2 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payload)));

        await Assert.That(body1.Equals(body2)).IsTrue();
    }

    [Test]
    public async Task When_two_bodies_have_different_bytes_should_not_be_equal()
    {
        var body1 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("{\"a\":1}")));
        var body2 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("{\"b\":2}")));

        await Assert.That(body1.Equals(body2)).IsFalse();
    }

    [Test]
    public async Task When_comparing_with_null_should_not_be_equal()
    {
        var body = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("test")));

        await Assert.That(body.Equals(null)).IsFalse();
    }

    [Test]
    public async Task When_using_equality_operator_with_identical_bytes_should_be_equal()
    {
        var payload = "hello world";
        var body1 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payload)));
        var body2 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payload)));

        await Assert.That(body1 == body2).IsTrue();
    }

    [Test]
    public async Task When_using_inequality_operator_with_different_bytes_should_not_be_equal()
    {
        var body1 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("foo")));
        var body2 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("bar")));

        await Assert.That(body1 != body2).IsTrue();
    }

    [Test]
    public async Task When_two_bodies_are_equal_should_have_same_hash_code()
    {
        var payload = "{\"key\":\"value\"}";
        var body1 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payload)));
        var body2 = new MessageBody(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payload)));

        await Assert.That(body1.GetHashCode()).IsEqualTo(body2.GetHashCode());
    }
}
