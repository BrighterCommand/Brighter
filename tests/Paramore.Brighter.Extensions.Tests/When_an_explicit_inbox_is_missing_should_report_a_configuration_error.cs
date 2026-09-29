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

namespace Paramore.Brighter.Extensions.Tests;

public class ConsumerMissingExplicitInboxTests
{
    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, false, true)]
    [Arguments(false, true, false)]
    [Arguments(false, true, true)]
    [Arguments(true, false, false)]
    [Arguments(true, false, true)]
    [Arguments(true, true, false)]
    [Arguments(true, true, true)]
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
            ? await Assert.That(() => processor.SendAsync(asyncCommand)).ThrowsExactly<ConfigurationException>()
            : (await Assert.That(() => processor.Send(command)).ThrowsExactly<ConfigurationException>());

        // Assert
        var diagnostic = exception.ToString();
        var inboxType = useAsyncHandler ? nameof(IAmAnInboxAsync) : nameof(IAmAnInboxSync);
        await Assert.That(diagnostic.Contains(inboxType) || diagnostic.Contains(nameof(InboxConfiguration))).IsTrue();
        await Assert.That(command.HandleCount).IsEqualTo(0);
        await Assert.That(asyncCommand.HandleCount).IsEqualTo(0);
    }
}
