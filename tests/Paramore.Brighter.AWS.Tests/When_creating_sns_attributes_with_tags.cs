using System.Collections.Generic;
using Amazon.SimpleNotificationService.Model;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWS.Tests;

[Property("Category", "AWS")]
public class When_creating_sns_attributes_with_tags
{
    [Test]
    public async Task When_tags_provided_should_store_them()
    {
        //arrange
        var tags = new List<Tag> { new() { Key = "Environment", Value = "Test" } };

        //act
        var snsAttributes = new SnsAttributes(tags: tags);

        //assert
        await Assert.That(snsAttributes.Tags).IsNotNull();
        await Assert.That(snsAttributes.Tags).HasSingleItem();
        await Assert.That(snsAttributes.Tags[0].Key).IsEqualTo("Environment");
        await Assert.That(snsAttributes.Tags[0].Value).IsEqualTo("Test");
    }

    [Test]
    public async Task When_no_tags_provided_should_be_empty()
    {
        //act
        var snsAttributes = new SnsAttributes();

        //assert
        await Assert.That(snsAttributes.Tags).IsNotNull();
        await Assert.That(snsAttributes.Tags).IsEmpty();
    }

    [Test]
    public async Task When_empty_should_have_empty_tags()
    {
        //act
        var snsAttributes = SnsAttributes.Empty;

        //assert
        await Assert.That(snsAttributes.Tags).IsNotNull();
        await Assert.That(snsAttributes.Tags).IsEmpty();
    }

    [Test]
    public async Task When_tags_provided_should_not_affect_other_parameters()
    {
        //arrange
        var tags = new List<Tag> { new() { Key = "Environment", Value = "Test" } };
        const string deliveryPolicy = "{\"http\":{\"defaultHealthyRetryPolicy\":{\"numRetries\":3}}}";
        const string policy = "{\"Version\":\"2012-10-17\"}";

        //act
        var snsAttributes = new SnsAttributes(
            deliveryPolicy: deliveryPolicy,
            policy: policy,
            type: SqsType.Fifo,
            contentBasedDeduplication: false,
            tags: tags
        );

        //assert
        await Assert.That(snsAttributes.DeliveryPolicy).IsEqualTo(deliveryPolicy);
        await Assert.That(snsAttributes.Policy).IsEqualTo(policy);
        await Assert.That(snsAttributes.Type).IsEqualTo(SqsType.Fifo);
        await Assert.That(snsAttributes.ContentBasedDeduplication).IsFalse();
        await Assert.That(snsAttributes.Tags).IsNotNull();
        await Assert.That(snsAttributes.Tags).HasSingleItem();
    }
}
