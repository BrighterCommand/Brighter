using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transformers.JustSaying;
using Paramore.Brighter.Transformers.JustSaying.JsonConverters;
using Xunit;

namespace Paramore.Brighter.Transforms.Adaptors.Tests.JustSaying;

/// <summary>
/// A JustSaying request that implements <see cref="IJustSayingRequest"/> directly, rather than
/// deriving from <c>JustSayingCommand</c> or <c>JustSayingEvent</c>. Those base classes carry
/// property-level <c>[JsonConverter]</c> attributes on Tenant and SourceIp; the interface does not,
/// and System.Text.Json does not inherit attributes from interface members. So a type in this shape
/// depends entirely on the converters the serializer options carry.
/// </summary>
public class JustSayingFrozenGlobalOptionsTests : IDisposable
{
    private const string TenantValue = "acme";
    private const string SourceIpValue = "10.1.2.3";
    private const string FallbackTenantValue = "fallback-tenant";

    private readonly JsonSerializerOptions _originalOptions;

    public JustSayingFrozenGlobalOptionsTests()
    {
        //Arrange
        //An application's own serialisation options carry Brighter's converters but not JustSaying's,
        //because nothing has registered them yet.
        _originalOptions = JsonSerialisationOptions.Options;

        var applicationOptions = new JsonSerializerOptions(_originalOptions);
        for (var i = applicationOptions.Converters.Count - 1; i >= 0; i--)
        {
            if (applicationOptions.Converters[i] is TenantConverter or IpAddressConverter)
            {
                applicationOptions.Converters.RemoveAt(i);
            }
        }

        //Brighter's outbox, inbox, scheduler and mediator all serialise through the global options
        //long before the first JustSaying message is mapped. That first use freezes them.
        JsonSerializer.Serialize(new { probe = "freezes the options" }, applicationOptions);

        JsonSerialisationOptions.Options = applicationOptions;
    }

    [Fact]
    public void When_mapping_a_direct_justsaying_request_after_the_global_options_are_frozen_should_write_flat_values()
    {
        //Arrange
        var mapper = new JustSayingMessageMapper<DirectJustSayingRequest>();
        var request = new DirectJustSayingRequest
        {
            Id = Guid.NewGuid().ToString(),
            Tenant = new Tenant(TenantValue),
            SourceIp = IPAddress.Parse(SourceIpValue)
        };

        //Act
        var message = mapper.MapToMessage(request, new Publication { Source = new Uri("http://test", UriKind.Absolute) });

        //Assert
        var doc = JsonNode.Parse(message.Body.Bytes, new JsonNodeOptions { PropertyNameCaseInsensitive = true });

        var tenantNode = doc![nameof(IJustSayingRequest.Tenant)];
        Assert.IsAssignableFrom<JsonValue>(tenantNode);
        Assert.Equal(TenantValue, tenantNode!.GetValue<string>());

        var sourceIpNode = doc[nameof(IJustSayingRequest.SourceIp)];
        Assert.IsAssignableFrom<JsonValue>(sourceIpNode);
        Assert.Equal(SourceIpValue, sourceIpNode!.GetValue<string>());
    }

    [Fact]
    public void When_mapping_a_justsaying_payload_to_a_request_after_the_global_options_are_frozen_should_read_flat_values()
    {
        //Arrange
        var mapper = new JustSayingMessageMapper<DirectJustSayingRequest>();

        //a genuine JustSaying wire payload, which writes both values as flat strings
        var body = $$"""{"tenant":"{{TenantValue}}","sourceIp":"{{SourceIpValue}}"}""";
        var message = new Message(new MessageHeader(), new MessageBody(body));

        //Act
        var request = mapper.MapToRequest(message);

        //Assert
        Assert.Equal(TenantValue, request.Tenant?.Value);
        Assert.Equal(SourceIpValue, request.SourceIp?.ToString());
    }

    [Fact]
    public void When_wrapping_a_message_mapped_after_the_global_options_are_frozen_should_keep_the_tenant_on_the_request()
    {
        //Arrange
        var mapper = new JustSayingMessageMapper<DirectJustSayingRequest>();
        var request = new DirectJustSayingRequest
        {
            Id = Guid.NewGuid().ToString(),
            Tenant = new Tenant(TenantValue)
        };

        var publication = new Publication { Source = new Uri("http://test", UriKind.Absolute) };
        var message = mapper.MapToMessage(request, publication);

        //the attribute carries a fallback tenant, which must not displace the one on the request
        var transform = new JustSayingTransform();
        transform.InitializeWrapFromAttributeParams(null, null, null, FallbackTenantValue, false);

        //Act
        var wrapped = transform.Wrap(message, publication);

        //Assert
        var doc = JsonNode.Parse(wrapped.Body.Bytes, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(TenantValue, doc![nameof(IJustSayingRequest.Tenant)]?.GetValue<string>());
    }

    public void Dispose() => JsonSerialisationOptions.Options = _originalOptions;

    public class DirectJustSayingRequest : IJustSayingRequest
    {
        public Id Id { get; set; } = Id.Random();
        public Id? CorrelationId { get; set; }
        public DateTimeOffset TimeStamp { get; set; }
        public string? RaisingComponent { get; set; }
        public string? Version { get; set; }
        public IPAddress? SourceIp { get; set; }
        public Tenant? Tenant { get; set; }
        public Id? Conversation { get; set; }
    }
}
