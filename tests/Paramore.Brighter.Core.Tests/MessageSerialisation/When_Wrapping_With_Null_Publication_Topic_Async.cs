using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

// Async twin of WrapNullPublicationTopicTests — pins the null-publication-topic
// guard in WrapPipelineAsync so refactors cannot silently NRE.
public class AsyncWrapNullPublicationTopicTests
{
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyResponse _myResponse;
    private readonly Publication _publication;

    public AsyncWrapNullPublicationTopicTests()
    {
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyResponseMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyResponse, MyResponseMessageMapperAsync>();

        var replyTopic = new RoutingKey(Uuid.NewAsString());
        var replyAddress = new ReplyAddress(replyTopic, Uuid.NewAsString());
        _myResponse = new MyResponse(replyAddress) { ReplyValue = "Hello World" };

        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync(_ => null);

        _publication = new Publication
        {
            Topic = null,
            RequestType = typeof(MyResponse)
        };

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory, InstrumentationOptions.All);
    }

    [Fact]
    public async Task When_Wrapping_With_Null_Publication_Topic_Async()
    {
        //act
        var transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyResponse>();
        var message = await transformPipeline.WrapAsync(_myResponse, new RequestContext(), _publication);

        //assert - topic came from the mapper
        Assert.Equal(_myResponse.SendersAddress.Topic, message.Header.Topic);

        //assert - no ProducerTopic entry was written to the bag (the guard held)
        Assert.False(message.Header.Bag.ContainsKey(Message.ProducerTopicHeaderName));
    }
}
