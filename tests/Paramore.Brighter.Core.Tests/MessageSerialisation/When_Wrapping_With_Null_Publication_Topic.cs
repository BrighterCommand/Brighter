using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

// Gap: WrapPipeline.Wrap now writes publication.Topic into Header.Bag when the
// mapper-set topic differs from the publication topic. The fix guards against a
// null publication.Topic before writing. Without this test, a future refactor that
// drops the null-check would NRE at runtime for publications that legitimately
// have no topic (dynamic routing scenarios).
public class WrapNullPublicationTopicTests
{
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyResponse _myResponse;
    private readonly Publication _publication;

    public WrapNullPublicationTopicTests()
    {
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyResponseMessageMapper()),
            null);
        mapperRegistry.Register<MyResponse, MyResponseMessageMapper>();

        var replyTopic = new RoutingKey(Uuid.NewAsString());
        var replyAddress = new ReplyAddress(replyTopic, Uuid.NewAsString());
        _myResponse = new MyResponse(replyAddress) { ReplyValue = "Hello World" };

        var messageTransformerFactory = new SimpleMessageTransformerFactory(_ => null);

        //publication with no topic — mapper still sets its own topic from the reply address
        _publication = new Publication
        {
            Topic = null,
            RequestType = typeof(MyResponse)
        };

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }

    [Fact]
    public void When_Wrapping_With_Null_Publication_Topic()
    {
        //act
        var transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyResponse>();
        var message = transformPipeline.Wrap(_myResponse, new RequestContext(), _publication);

        //assert - topic came from the mapper
        Assert.Equal(_myResponse.SendersAddress.Topic, message.Header.Topic);

        //assert - no ProducerTopic entry was written to the bag (the guard held)
        Assert.False(message.Header.Bag.ContainsKey(Message.ProducerTopicHeaderName));
    }
}
