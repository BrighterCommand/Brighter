#region Licence
/* The MIT License (MIT)

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

using System.Linq;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class OutboxArchiverNonGenericRegistrationTests
{
    [Fact]
    public void When_using_non_generic_outbox_archiver_should_resolve_transaction_type()
    {
        // Arrange
        var services = new ServiceCollection();
        var brighterBuilder = services.AddBrighter();

        services.AddSingleton(typeof(IAmABoxTransactionProvider<CommittableTransaction>), typeof(InMemoryTransactionProvider));

        // Act
        brighterBuilder.UseOutboxArchiver(new NullOutboxArchiveProvider());

        // Assert — OutboxArchiver and TimedOutboxArchiver registered with the correct transaction type
        var archiverDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(OutboxArchiver<Message, CommittableTransaction>));
        Assert.NotNull(archiverDescriptor);

        var hostedServiceDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(TimedOutboxArchiver<Message, CommittableTransaction>));
        Assert.NotNull(hostedServiceDescriptor);
    }

    [Fact]
    public void When_no_transaction_provider_registered_should_throw_configuration_exception()
    {
        // Arrange
        var services = new ServiceCollection();
        var brighterBuilder = services.AddBrighter();

        // Act / Assert — no IAmABoxTransactionProvider<> registered
        Assert.Throws<ConfigurationException>(() =>
            brighterBuilder.UseOutboxArchiver(new NullOutboxArchiveProvider()));
    }
}
