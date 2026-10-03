#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

#nullable enable

using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class DefaultMapperRoundTripTests
{
    public static TheoryData<bool, string?> RequestCases => new()
    {
        { false, "Hello" },
        { true, "Hello" },
        { false, "" },
        { true, "" },
        { false, null },
        { true, null }
    };

    [Theory]
    [MemberData(nameof(RequestCases))]
    public void When_default_mapper_should_round_trip_requests_as_plain_json(bool addConsumers, string? text)
    {
        //Arrange
        using var provider = BuildProvider(addConsumers);
        using var registry = ServiceCollectionExtensions.MessageMapperRegistry(provider);
        var request = new DefaultMapperEvent { Text = text, CorrelationId = Id.Random() };
        var publication = new Publication { Topic = new RoutingKey("default-mapper.event") };
        var mapper = registry.Get<DefaultMapperEvent>();

        try
        {
            Assert.NotNull(mapper);
            mapper.Instance.Context = new RequestContext();

            //Act
            var message = mapper.Instance.MapToMessage(request, publication);
            var restored = mapper.Instance.MapToRequest(message);

            //Assert
            AssertPlainJson(message, request);
            Assert.Equal(request.Text, restored.Text);
            Assert.Equal(request.Id, restored.Id);
            Assert.Equal(request.CorrelationId, restored.CorrelationId);
        }
        finally
        {
            registry.Release(mapper);
        }
    }

    [Theory]
    [MemberData(nameof(RequestCases))]
    public async Task When_default_async_mapper_should_round_trip_requests_as_plain_json(bool addConsumers, string? text)
    {
        //Arrange
        await using var provider = BuildProvider(addConsumers);
        using var registry = ServiceCollectionExtensions.MessageMapperRegistry(provider);
        var request = new DefaultMapperEvent { Text = text, CorrelationId = Id.Random() };
        var publication = new Publication { Topic = new RoutingKey("default-mapper.event") };
        var mapper = registry.GetAsync<DefaultMapperEvent>();

        try
        {
            Assert.NotNull(mapper);
            mapper.Instance.Context = new RequestContext();

            //Act
            var message = await mapper.Instance.MapToMessageAsync(request, publication);
            var restored = await mapper.Instance.MapToRequestAsync(message);

            //Assert
            AssertPlainJson(message, request);
            Assert.Equal(request.Text, restored.Text);
            Assert.Equal(request.Id, restored.Id);
            Assert.Equal(request.CorrelationId, restored.CorrelationId);
        }
        finally
        {
            await registry.ReleaseAsync(mapper);
        }
    }

    private static ServiceProvider BuildProvider(bool addConsumers)
    {
        var services = new ServiceCollection();
        var builder = addConsumers ? services.AddConsumers() : services.AddBrighter();
        builder.MapperRegistry(_ => { });
        return services.BuildServiceProvider();
    }

    private static void AssertPlainJson(Message message, DefaultMapperEvent request)
    {
        Assert.Equal("application/json", message.Header.ContentType.MediaType);
        using var json = JsonDocument.Parse(message.Body.Memory);
        Assert.Equal(request.Text, json.RootElement.GetProperty("text").GetString());
        Assert.False(json.RootElement.TryGetProperty("specversion", out _));
        Assert.False(json.RootElement.TryGetProperty("data", out _));
    }
}
