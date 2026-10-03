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

using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.Sqlite;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests.RelationalConfiguration;

public class RelationalOutboxConfigurationFallbackTests
{
    [Theory]
    [InlineData(ServiceLifetime.Transient)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Singleton)]
    public void When_configuring_relational_producers_should_reuse_outbox_configuration(ServiceLifetime lifetime)
    {
        //Arrange
        var configuration = new RelationalDatabaseConfiguration("Data Source=:memory:");
        var outbox = new SqliteOutbox(configuration);
        var services = new ServiceCollection();
        services.AddBrighter().AddProducers(options =>
        {
            options.Outbox = outbox;
            options.TransactionProvider = typeof(SqliteTransactionProvider);
            options.ConnectionProvider = typeof(SqliteConnectionProvider);
        }, lifetime);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        //Act
        var resolvedConfiguration = firstScope.ServiceProvider.GetService<IAmARelationalDatabaseConfiguration>();

        //Assert
        Assert.Same(configuration, resolvedConfiguration);
        Assert.Same(configuration, secondScope.ServiceProvider.GetRequiredService<IAmARelationalDatabaseConfiguration>());
        Assert.Same(outbox, provider.GetRequiredService<IAmAnOutbox>());
        Assert.NotNull(provider.GetRequiredService<IAmACommandProcessor>());
        Assert.IsType<SqliteTransactionProvider>(firstScope.ServiceProvider.GetRequiredService<IAmABoxTransactionProvider<DbTransaction>>());
        Assert.IsType<SqliteTransactionProvider>(firstScope.ServiceProvider.GetRequiredService<IAmATransactionConnectionProvider>());

        var firstTransaction = firstScope.ServiceProvider.GetRequiredService<IAmABoxTransactionProvider>();
        var repeatedTransaction = firstScope.ServiceProvider.GetRequiredService<IAmABoxTransactionProvider>();
        var secondTransaction = secondScope.ServiceProvider.GetRequiredService<IAmABoxTransactionProvider>();
        var firstConnection = firstScope.ServiceProvider.GetRequiredService<IAmARelationalDbConnectionProvider>();
        var repeatedConnection = firstScope.ServiceProvider.GetRequiredService<IAmARelationalDbConnectionProvider>();
        var secondConnection = secondScope.ServiceProvider.GetRequiredService<IAmARelationalDbConnectionProvider>();
        Assert.IsType<SqliteTransactionProvider>(firstTransaction);
        Assert.IsType<SqliteConnectionProvider>(firstConnection);

        if (lifetime == ServiceLifetime.Transient)
        {
            Assert.NotSame(firstTransaction, repeatedTransaction);
            Assert.NotSame(firstConnection, repeatedConnection);
        }
        else
        {
            Assert.Same(firstTransaction, repeatedTransaction);
            Assert.Same(firstConnection, repeatedConnection);
        }

        if (lifetime == ServiceLifetime.Singleton)
        {
            Assert.Same(firstTransaction, secondTransaction);
            Assert.Same(firstConnection, secondConnection);
        }
        else
        {
            Assert.NotSame(firstTransaction, secondTransaction);
            Assert.NotSame(firstConnection, secondConnection);
        }
    }
}
