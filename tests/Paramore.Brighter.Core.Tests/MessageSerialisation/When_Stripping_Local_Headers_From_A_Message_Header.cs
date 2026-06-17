using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class MessageHeaderLocalHeadersTests
{
    [Fact]
    public void When_BagWithoutLocalHeaders_Removes_Local_Entries_And_Other_Entries_Survive()
    {
        var header = new MessageHeader(
            messageId: "id-1",
            topic: new RoutingKey("a.topic"),
            messageType: MessageType.MT_COMMAND);
        header.Bag[Message.ProducerTopicHeaderName] = "lookup.topic";
        header.Bag["user.key"] = "user.value";

        var wireBag = header.BagWithoutLocalHeaders();

        Assert.False(wireBag.ContainsKey(Message.ProducerTopicHeaderName));
        Assert.True(wireBag.ContainsKey("user.key"));
        // original is untouched — InMemoryOutbox-by-reference keeps the local entry for retries
        Assert.True(header.Bag.ContainsKey(Message.ProducerTopicHeaderName));
    }

    [Fact]
    public void When_BagWithoutLocalHeaders_With_No_Local_Entries_Returns_Equivalent_Copy()
    {
        var header = new MessageHeader(
            messageId: "id-2",
            topic: new RoutingKey("a.topic"),
            messageType: MessageType.MT_EVENT);
        header.Bag["user.key"] = "user.value";

        var wireBag = header.BagWithoutLocalHeaders();

        Assert.Single(wireBag);
        Assert.Equal("user.value", wireBag["user.key"]);
    }

    [Fact]
    public void When_The_Producer_Topic_Header_Name_Is_A_Local_Header()
    {
        Assert.True(MessageHeader.IsLocalHeader(Message.ProducerTopicHeaderName));
    }

    [Fact]
    public void When_RegisterLocalHeader_Adds_A_Custom_Key_It_Is_Recognised_And_Is_Idempotent()
    {
        const string customKey = "custom.local.header." + nameof(MessageHeaderLocalHeadersTests);

        MessageHeader.RegisterLocalHeader(customKey);
        MessageHeader.RegisterLocalHeader(customKey); // idempotent

        Assert.True(MessageHeader.IsLocalHeader(customKey));

        var header = new MessageHeader(
            messageId: "id-3",
            topic: new RoutingKey("a.topic"),
            messageType: MessageType.MT_COMMAND);
        header.Bag[customKey] = "value";
        header.Bag["user.key"] = "user.value";

        var wireBag = header.BagWithoutLocalHeaders();

        Assert.False(wireBag.ContainsKey(customKey));
        Assert.True(wireBag.ContainsKey("user.key"));
    }
}
