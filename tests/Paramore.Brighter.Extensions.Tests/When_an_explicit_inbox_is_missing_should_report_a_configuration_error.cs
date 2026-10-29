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

using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ConsumerMissingExplicitInboxTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task When_an_explicit_inbox_is_missing_should_report_a_configuration_error(
        bool useOptionsFactory, bool useAsyncHandler, bool useIncompatibleInbox)
    {
        // Arrange
        var configuration = useIncompatibleInbox
            ? new InboxConfiguration(useAsyncHandler ? A.Fake<IAmAnInboxSync>() : A.Fake<IAmAnInboxAsync>())
            : null;
        var services = new ServiceCollection();
        if (useOptionsFactory)
            services.AddConsumers(_ => new ConsumersOptions { InboxConfiguration = configuration });
        else
            services.AddConsumers(options => { options.InboxConfiguration = configuration; });

        using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var command = new ConsumerExplicitInboxCommand();
        var asyncCommand = new ConsumerExplicitInboxAsyncCommand();

        // Act
        var exception = useAsyncHandler
            ? await Assert.ThrowsAsync<ConfigurationException>(() => processor.SendAsync(asyncCommand))
            : Assert.Throws<ConfigurationException>(() => processor.Send(command));

        // Assert
        var diagnostic = exception.ToString();
        var inboxType = useAsyncHandler ? nameof(IAmAnInboxAsync) : nameof(IAmAnInboxSync);
        Assert.True(diagnostic.Contains(inboxType) || diagnostic.Contains(nameof(InboxConfiguration)), diagnostic);
        Assert.Equal(0, command.HandleCount);
        Assert.Equal(0, asyncCommand.HandleCount);
    }
}
