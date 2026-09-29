namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class MessageHeaderLocalHeadersTests
{
    [Test]
    public async Task When_BagWithoutLocalHeaders_Removes_Local_Entries_And_Other_Entries_Survive()
    {
        var header = new MessageHeader(
            messageId: "id-1",
            topic: new RoutingKey("a.topic"),
            messageType: MessageType.MT_COMMAND);
        header.Bag[Message.ProducerTopicHeaderName] = "lookup.topic";
        header.Bag["user.key"] = "user.value";

        var wireBag = header.BagWithoutLocalHeaders();

        await Assert.That(wireBag.ContainsKey(Message.ProducerTopicHeaderName)).IsFalse();
        await Assert.That(wireBag.ContainsKey("user.key")).IsTrue();
        // original is untouched — InMemoryOutbox-by-reference keeps the local entry for retries
        await Assert.That(header.Bag.ContainsKey(Message.ProducerTopicHeaderName)).IsTrue();
    }

    [Test]
    public async Task When_BagWithoutLocalHeaders_With_No_Local_Entries_Returns_Equivalent_Copy()
    {
        var header = new MessageHeader(
            messageId: "id-2",
            topic: new RoutingKey("a.topic"),
            messageType: MessageType.MT_EVENT);
        header.Bag["user.key"] = "user.value";

        var wireBag = header.BagWithoutLocalHeaders();

        await Assert.That(wireBag).HasSingleItem();
        await Assert.That(wireBag["user.key"]).IsEqualTo("user.value");
    }

    [Test]
    public async Task When_The_Producer_Topic_Header_Name_Is_A_Local_Header()
    {
        await Assert.That(MessageHeader.IsLocalHeader(Message.ProducerTopicHeaderName)).IsTrue();
    }

    [Test]
    public async Task When_RegisterLocalHeader_Adds_A_Custom_Key_It_Is_Recognised_And_Is_Idempotent()
    {
        const string customKey = "custom.local.header." + nameof(MessageHeaderLocalHeadersTests);

        MessageHeader.RegisterLocalHeader(customKey);
        MessageHeader.RegisterLocalHeader(customKey); // idempotent

        await Assert.That(MessageHeader.IsLocalHeader(customKey)).IsTrue();

        var header = new MessageHeader(
            messageId: "id-3",
            topic: new RoutingKey("a.topic"),
            messageType: MessageType.MT_COMMAND);
        header.Bag[customKey] = "value";
        header.Bag["user.key"] = "user.value";

        var wireBag = header.BagWithoutLocalHeaders();

        await Assert.That(wireBag.ContainsKey(customKey)).IsFalse();
        await Assert.That(wireBag.ContainsKey("user.key")).IsTrue();
    }
}
