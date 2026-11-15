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

using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Control.Api;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

public class ControlApiSubscriptionNameTests
{
    [Theory]
    [InlineData("orders-subscription", "orders-subscription", HttpStatusCode.OK, 0)]
    [InlineData("orders-subscription", "ORDERS-SUBSCRIPTION", HttpStatusCode.BadRequest, 0)]
    [InlineData("orders-subscription", "Orders-Subscription", HttpStatusCode.BadRequest, 0)]
    [InlineData("ORDERS-SUBSCRIPTION", "ORDERS-SUBSCRIPTION", HttpStatusCode.OK, 0)]
    [InlineData("ORDERS-SUBSCRIPTION", "orders-subscription", HttpStatusCode.BadRequest, 0)]
    [InlineData("orders-subscription", "unknown-subscription", HttpStatusCode.BadRequest, 0)]
    [InlineData("orders-subscription", "ORDERS-SUBSCRIPTION", HttpStatusCode.BadRequest, 3)]
    public async Task When_setting_performers_should_match_the_subscription_name_exactly(
        string registeredName, string requestedName, HttpStatusCode expectedStatus, int requestedPerformers)
    {
        // Arrange
        var subscription = new Subscription(
            new SubscriptionName(registeredName), new ChannelName("orders-channel"), new RoutingKey("orders"),
            requestType: typeof(Command), noOfPerformers: 0, messagePumpType: MessagePumpType.Reactor);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(Initializer.Factory);
        builder.Services.AddBrighter();
        builder.Services.AddSingleton<IDispatcher>(provider => new Dispatcher(
            provider.GetRequiredService<IAmACommandProcessor>(), [subscription], Initializer.Factory,
            ServiceCollectionExtensions.MessageMapperRegistry(provider), ownsRegistry: true));
        await using var application = builder.Build();
        application.MapBrighterControlEndpoints();
        await application.StartAsync();
        using var client = application.GetTestClient();

        // Act
        using var response = await client.PatchAsync($"/control/subscriptions/{requestedName}/performers/{requestedPerformers}", null);

        // Assert
        Assert.Equal(expectedStatus, response.StatusCode);
        var expectedMessage = expectedStatus == HttpStatusCode.OK
            ? $"Active performers for {requestedName} set to {requestedPerformers}"
            : $"No such subscription {requestedName}";
        Assert.Equal(expectedMessage, await response.Content.ReadFromJsonAsync<string>());
        Assert.Equal(0, subscription.NoOfPerformers);
    }
}
