using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

// Fallback path for OutboxProducerMediator.GetProducerLookupTopic: when a mapper does
// NOT override Header.Topic (the normal-publication case), no ProducerTopic bag entry
// is written. Producer lookup then falls back to Header.Topic. Without this pin, a
// future change that always wrote the bag entry would leave persistent outbox rows
// across a rolling upgrade with stale producer hints — exactly the failure mode the
// pre-fix bug had, just inverted.
public class WrapMatchingPublicationTopicTests
{
    [Test]
    public async Task When_The_Mapper_Topic_Matches_The_Publication_No_Producer_Topic_Bag_Entry_Is_Written()
    {
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()),
            null);
        mapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

        var publicationTopic = new RoutingKey("normal.publication.topic");
        var publication = new Publication
        {
            Topic = publicationTopic,
            RequestType = typeof(MyCommand)
        };

        var pipelineBuilder = new TransformPipelineBuilder(
            mapperRegistry,
            new SimpleMessageTransformerFactory(_ => null));

        var message = pipelineBuilder
            .BuildWrapPipeline<MyCommand>()
            .Wrap(new MyCommand(), new RequestContext(), publication);

        // mapper produced a topic identical to publication.Topic — fallback path
        await Assert.That(message.Header.Topic).IsEqualTo(publicationTopic);

        // no bag entry → producer lookup falls back to Header.Topic
        await Assert.That(message.Header.Bag.ContainsKey(Message.ProducerTopicHeaderName)).IsFalse();
    }
}
