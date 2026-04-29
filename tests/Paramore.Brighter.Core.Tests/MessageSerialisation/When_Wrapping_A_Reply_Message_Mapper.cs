using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class ReplyMessageWrapRequestTests
{
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyResponse _myResponse;
    private readonly Publication _publication;

    public ReplyMessageWrapRequestTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyResponseMessageMapper()),
            null);
        mapperRegistry.Register<MyResponse, MyResponseMessageMapper>();

        var replyTopic = new RoutingKey(Uuid.NewAsString());
        var replyAddress = new ReplyAddress(replyTopic, Uuid.NewAsString());
        _myResponse = new MyResponse(replyAddress) { ReplyValue = "Hello World" };

        var messageTransformerFactory = new SimpleMessageTransformerFactory(_ => null);

        _publication = new Publication
        {
            Topic = new RoutingKey("Reply"),
            RequestType = typeof(MyResponse)
        };

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }

    [Fact]
    public void When_Wrapping_A_Reply_Message_Mapper()
    {
        //act
        var transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyResponse>();
        var message = transformPipeline.Wrap(_myResponse, new RequestContext(), _publication);

        //assert - message topic is the reply address, not the publication topic
        Assert.Equal(_myResponse.SendersAddress.Topic, message.Header.Topic);
        Assert.NotEqual(_publication.Topic, message.Header.Topic);

        //assert - publication topic stored in bag for producer lookup
        Assert.True(message.Header.Bag.ContainsKey(Message.ProducerTopicHeaderName));
        Assert.Equal(_publication.Topic!.Value, message.Header.Bag[Message.ProducerTopicHeaderName]);
    }
}
