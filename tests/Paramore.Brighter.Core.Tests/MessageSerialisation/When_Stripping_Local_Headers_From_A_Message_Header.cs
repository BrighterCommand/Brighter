using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class MessageHeaderStripLocalHeadersTests
{
    [Fact]
    public void When_Stripping_Local_Headers_The_ProducerTopic_Bag_Entry_Is_Removed_And_Other_Entries_Survive()
    {
        var header = new MessageHeader(
            messageId: "id-1",
            topic: new RoutingKey("a.topic"),
            messageType: MessageType.MT_COMMAND);
        header.Bag[Message.ProducerTopicHeaderName] = "lookup.topic";
        header.Bag["user.key"] = "user.value";

        header.StripLocalHeaders();

        Assert.False(header.Bag.ContainsKey(Message.ProducerTopicHeaderName));
        Assert.True(header.Bag.ContainsKey("user.key"));
    }

    [Fact]
    public void When_Stripping_Local_Headers_With_No_Local_Bag_Entries_Then_The_Bag_Is_Unchanged()
    {
        var header = new MessageHeader(
            messageId: "id-2",
            topic: new RoutingKey("a.topic"),
            messageType: MessageType.MT_EVENT);
        header.Bag["user.key"] = "user.value";

        header.StripLocalHeaders();

        Assert.Single(header.Bag);
        Assert.Equal("user.value", header.Bag["user.key"]);
    }

    [Fact]
    public void When_The_Producer_Topic_Header_Name_Is_In_The_Local_Header_Names_Set()
    {
        Assert.Contains(Message.ProducerTopicHeaderName, MessageHeader.LocalHeaderNames);
    }
}
