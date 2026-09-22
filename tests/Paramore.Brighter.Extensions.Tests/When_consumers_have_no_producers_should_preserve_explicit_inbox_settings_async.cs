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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ConsumerExplicitInboxAsyncTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_consumers_have_no_producers_should_preserve_explicit_inbox_settings_async(bool useOptionsFactory)
    {
        // Arrange
        var inbox = new InMemoryInbox(TimeProvider.System);
        var configuration = new InboxConfiguration(inbox,
            actionOnExists: OnceOnlyAction.Throw, context: _ => "global-inbox");
        var services = new ServiceCollection();
        IBrighterBuilder builder;
        if (useOptionsFactory)
            builder = services.AddConsumers(_ => new ConsumersOptions { InboxConfiguration = configuration });
        else
            builder = services.AddConsumers(options => { options.InboxConfiguration = configuration; });

        builder.ValidatePipelines();

        using var provider = services.BuildServiceProvider();
        var validation = provider.GetRequiredService<IAmAPipelineValidator>().Validate();
        Assert.Empty(validation.Errors);
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var command = new ConsumerExplicitInboxAsyncCommand();

        // Act
        await processor.SendAsync(command);
        await processor.SendAsync(command);

        // Assert
        Assert.Equal(1, command.HandleCount);
        Assert.True(await inbox.ExistsAsync<ConsumerExplicitInboxAsyncCommand>(command.Id, "explicit-inbox", null));
        Assert.False(await inbox.ExistsAsync<ConsumerExplicitInboxAsyncCommand>(command.Id, "global-inbox", null));
    }
}
