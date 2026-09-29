#region Licence

/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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

using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.Sqlite;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests.RelationalConfiguration;

public class DeferredRelationalConfigurationTests
{
    [Fact]
    public void When_deferring_producer_configuration_should_preserve_optional_database_lookup()
    {
        //Arrange
        var services = new ServiceCollection();
        var configuration = new RelationalDatabaseConfiguration("Data Source=:memory:");
        SqliteOutbox? outbox = null;
        var factoryCalls = 0;

        //Act
        services.AddBrighter().AddProducers(sp =>
        {
            factoryCalls++;
            var databaseConfiguration = sp.GetService<IAmARelationalDatabaseConfiguration>() ?? configuration;
            outbox = new SqliteOutbox(databaseConfiguration);
            return new ProducersConfiguration
            {
                Outbox = outbox,
                TransactionProvider = typeof(SqliteTransactionProvider)
            };
        });
        using var provider = services.BuildServiceProvider();

        //Assert
        Assert.Equal(0, factoryCalls);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IAmARelationalDatabaseConfiguration));
        var resolvedConfiguration = provider.GetRequiredService<IAmProducersConfiguration>();
        Assert.NotNull(outbox);
        Assert.Same(outbox, resolvedConfiguration.Outbox);
        Assert.Equal(1, factoryCalls);
        Assert.Same(resolvedConfiguration, provider.GetRequiredService<IAmProducersConfiguration>());
        Assert.Equal(1, factoryCalls);
    }
}
