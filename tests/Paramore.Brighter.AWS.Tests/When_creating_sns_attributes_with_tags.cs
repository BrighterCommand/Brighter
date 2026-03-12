using System.Collections.Generic;
using Amazon.SimpleNotificationService.Model;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests;

public class When_creating_sns_attributes_with_tags
{
    [Fact]
    public void When_tags_provided_should_store_them()
    {
        //arrange
        var tags = new List<Tag> { new() { Key = "Environment", Value = "Test" } };

        //act
        var snsAttributes = new SnsAttributes(tags: tags);

        //assert
        Assert.NotNull(snsAttributes.Tags);
        Assert.Single(snsAttributes.Tags);
        Assert.Equal("Environment", snsAttributes.Tags[0].Key);
        Assert.Equal("Test", snsAttributes.Tags[0].Value);
    }

    [Fact]
    public void When_no_tags_provided_should_be_empty()
    {
        //act
        var snsAttributes = new SnsAttributes();

        //assert
        Assert.NotNull(snsAttributes.Tags);
        Assert.Empty(snsAttributes.Tags);
    }

    [Fact]
    public void When_empty_should_have_empty_tags()
    {
        //act
        var snsAttributes = SnsAttributes.Empty;

        //assert
        Assert.NotNull(snsAttributes.Tags);
        Assert.Empty(snsAttributes.Tags);
    }

    [Fact]
    public void When_tags_provided_should_not_affect_other_parameters()
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
        Assert.Equal(deliveryPolicy, snsAttributes.DeliveryPolicy);
        Assert.Equal(policy, snsAttributes.Policy);
        Assert.Equal(SqsType.Fifo, snsAttributes.Type);
        Assert.False(snsAttributes.ContentBasedDeduplication);
        Assert.NotNull(snsAttributes.Tags);
        Assert.Single(snsAttributes.Tags);
    }
}
