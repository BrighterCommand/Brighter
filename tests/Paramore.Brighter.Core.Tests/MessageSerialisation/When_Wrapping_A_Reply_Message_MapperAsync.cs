using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class AsyncReplyMessageWrapRequestTests
{
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyResponse _myResponse;
    private readonly Publication _publication;

    public AsyncReplyMessageWrapRequestTests()
    {
        //arrange
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
            Topic = new RoutingKey("Reply"),
            RequestType = typeof(MyResponse)
        };

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory, InstrumentationOptions.All);
    }

    [Test]
    public async Task When_Wrapping_A_Reply_Message_Mapper_Async()
    {
        //act
        var transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyResponse>();
        var message = await transformPipeline.WrapAsync(_myResponse, new RequestContext(), _publication);

        //assert - message topic is the reply address, not the publication topic
        await Assert.That(message.Header.Topic).IsEqualTo(_myResponse.SendersAddress.Topic);
        await Assert.That(message.Header.Topic).IsNotEqualTo(_publication.Topic);

        //assert - publication topic stored in bag for producer lookup
        await Assert.That(message.Header.Bag.ContainsKey(Message.ProducerTopicHeaderName)).IsTrue();
        await Assert.That(message.Header.Bag[Message.ProducerTopicHeaderName]).IsEqualTo(_publication.Topic!.Value);
    }
}
