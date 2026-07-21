using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Paramore.Brighter.Transformers.JustSaying;

namespace Paramore.Brighter.Transforms.Adaptors.Tests.JustSaying;

public class JustSayingTransformTest
{
    private readonly JustSayingTransform _transform = new();

    [Test]
    public async Task wrap_should_set_properties_when_it_missing()
    {
        var raisingComponent = $"RaisingComponent{Guid.NewGuid()}";
        var version = $"Version{Guid.NewGuid()}";
        var subject = $"Subject{Guid.NewGuid()}";
        var tenant = $"Tenant{Guid.NewGuid()}";
        var correlationId = Guid.NewGuid().ToString();
        var timeStamp = DateTimeOffset.UtcNow;
        
        _transform.InitializeWrapFromAttributeParams(raisingComponent, version, subject, tenant, false);

        var message = new Message(new MessageHeader
        {
            CorrelationId = correlationId,
            TimeStamp = timeStamp 
        }, new MessageBody("{ \"test\": \"some-value \"}"));
        
        message = _transform.Wrap(message, new Publication());

        var node = JsonNode.Parse(message.Body.Bytes,
                new JsonNodeOptions { PropertyNameCaseInsensitive = true }, 
                new JsonDocumentOptions { MaxDepth = 0 });
        
        await Assert.That(message.Header.Subject).IsEqualTo(subject);
        
        await Assert.That(node).IsNotNull();
        
        await Assert.That(node[nameof(IJustSayingRequest.Id)]).IsNotNull();
        await Assert.That(node[nameof(IJustSayingRequest.TimeStamp)]).IsNotNull();
        
        await Assert.That(node[nameof(IJustSayingRequest.RaisingComponent)]).IsNotNull();
        await Assert.That(node[nameof(IJustSayingRequest.RaisingComponent)]!.GetValue<string>()).IsEqualTo(raisingComponent);
        
        await Assert.That(node[nameof(IJustSayingRequest.Version)]).IsNotNull();
        await Assert.That(node[nameof(IJustSayingRequest.Version)]!.GetValue<string>()).IsEqualTo(version);
        
        await Assert.That(node[nameof(IJustSayingRequest.Tenant)]).IsNotNull();
        await Assert.That(node[nameof(IJustSayingRequest.Tenant)]!.GetValue<string>()).IsEqualTo(tenant);
        
        await Assert.That(node[nameof(IJustSayingRequest.Conversation)]).IsNotNull();
        await Assert.That(node[nameof(IJustSayingRequest.Conversation)]!.GetValue<string>()).IsEqualTo(correlationId);
    }
    
    [Test]
    public async Task wrap_should_set_properties_from_request_context_when_it_missing()
    {
        var raisingComponent = $"RaisingComponent{Guid.NewGuid()}";
        var version = $"Version{Guid.NewGuid()}";
        var subject = $"Subject{Guid.NewGuid()}";
        var tenant = $"Tenant{Guid.NewGuid()}";
        
        _transform.InitializeWrapFromAttributeParams(null, null, null, null, false);

        _transform.Context = new RequestContext
        {
            Bag =
            {
                [JustSayingAttributesName.RaisingComponent] = raisingComponent,
                [JustSayingAttributesName.Subject] = subject,
                [JustSayingAttributesName.Tenant] = tenant,
                [JustSayingAttributesName.Version] = version
            }
        };

        var message = new Message(new MessageHeader(), new MessageBody("{ \"test\": \"some-value \"}"));
        
        message = _transform.Wrap(message, new Publication());

        var node = JsonNode.Parse(message.Body.Bytes,
            new JsonNodeOptions { PropertyNameCaseInsensitive = true }, 
            new JsonDocumentOptions { MaxDepth = 0 });
        
        
        await Assert.That(node).IsNotNull();
        
        await Assert.That(node[nameof(IJustSayingRequest.Id)]).IsNotNull();
        await Assert.That(node[nameof(IJustSayingRequest.Conversation)]).IsNotNull();
        await Assert.That(node[nameof(IJustSayingRequest.TimeStamp)]).IsNotNull();
        
        await Assert.That(node[nameof(IJustSayingRequest.RaisingComponent)]).IsNotNull();
        await Assert.That(node[nameof(IJustSayingRequest.RaisingComponent)]!.GetValue<string>()).IsEqualTo(raisingComponent);
        
        await Assert.That(node[nameof(IJustSayingRequest.Version)]).IsNotNull();
        await Assert.That(node[nameof(IJustSayingRequest.Version)]!.GetValue<string>()).IsEqualTo(version);
        
        await Assert.That(node[nameof(IJustSayingRequest.Tenant)]).IsNotNull();
        await Assert.That(node[nameof(IJustSayingRequest.Tenant)]!.GetValue<string>()).IsEqualTo(tenant);
        
    }
    
    [Test]
    public async Task wrap_should_not_override_properties_when_it_is_in_payload()
    {
        var raisingComponent = $"RaisingComponent{Guid.NewGuid()}";
        var version = $"Version{Guid.NewGuid()}";
        var subject = $"Subject{Guid.NewGuid()}";
        var tenant = $"Tenant{Guid.NewGuid()}";
        var timeStamp = DateTimeOffset.UtcNow;
        
        _transform.InitializeWrapFromAttributeParams(raisingComponent, version, subject, tenant, false);

        var message = new Message(new MessageHeader { CorrelationId = Id.Empty, TimeStamp = timeStamp },
            new MessageBody(
                """
                { 
                    "id": "1a0eec22-6f72-4281-a189-1024b1b3ec17",
                    "conversation": "4bf0aa5d-40e7-4239-be3f-18cb317a0eeb", 
                    "raisingComponent": "brighter-test", 
                    "tenant": "uk",
                    "timeStamp": "2025-06-20T08:29:24+00:00",
                    "version": "1.2.3",
                    "test": "some-value"
                }
                """
                ));
        
        message = _transform.Wrap(message, new Publication());

        var node = JsonNode.Parse(message.Body.Bytes,
            new JsonNodeOptions { PropertyNameCaseInsensitive = true }, 
            new JsonDocumentOptions { MaxDepth = 0 });
        
        await Assert.That(node).IsNotNull();
        
        await Assert.That(node[nameof(IJustSayingRequest.Id)]).IsNotNull();
        await Assert.That(node[nameof(IJustSayingRequest.Conversation)]).IsNotNull();
        await Assert.That(node[nameof(IJustSayingRequest.TimeStamp)]).IsNotNull();
        
        await Assert.That(node[nameof(IJustSayingRequest.RaisingComponent)]).IsNotNull();
        await Assert.That(node[nameof(IJustSayingRequest.RaisingComponent)]!.GetValue<string>()).IsNotEqualTo(raisingComponent);
        
        await Assert.That(node[nameof(IJustSayingRequest.Version)]).IsNotNull();
        await Assert.That(node[nameof(IJustSayingRequest.Version)]!.GetValue<string>()).IsNotEqualTo(version);
        
        await Assert.That(node[nameof(IJustSayingRequest.Tenant)]).IsNotNull();
        await Assert.That(node[nameof(IJustSayingRequest.Tenant)]!.GetValue<string>()).IsNotEqualTo(tenant);
        
    }
}
