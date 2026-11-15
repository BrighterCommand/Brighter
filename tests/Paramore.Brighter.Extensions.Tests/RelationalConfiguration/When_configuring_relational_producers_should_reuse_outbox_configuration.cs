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

namespace Paramore.Brighter.Extensions.Tests.RelationalConfiguration;

public class RelationalOutboxConfigurationFallbackTests
{
    [Test]
    [Arguments(ServiceLifetime.Transient)]
    [Arguments(ServiceLifetime.Scoped)]
    [Arguments(ServiceLifetime.Singleton)]
    public async Task When_configuring_relational_producers_should_reuse_outbox_configuration(ServiceLifetime lifetime)
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
        await Assert.That(resolvedConfiguration).IsSameReferenceAs(configuration);
        await Assert.That(secondScope.ServiceProvider.GetRequiredService<IAmARelationalDatabaseConfiguration>()).IsSameReferenceAs(configuration);
        await Assert.That(provider.GetRequiredService<IAmAnOutbox>()).IsSameReferenceAs(outbox);
        await Assert.That(provider.GetRequiredService<IAmACommandProcessor>()).IsNotNull();
        await Assert.That(firstScope.ServiceProvider.GetRequiredService<IAmABoxTransactionProvider<DbTransaction>>()).IsTypeOf<SqliteTransactionProvider>();
        await Assert.That(firstScope.ServiceProvider.GetRequiredService<IAmATransactionConnectionProvider>()).IsTypeOf<SqliteTransactionProvider>();

        var firstTransaction = firstScope.ServiceProvider.GetRequiredService<IAmABoxTransactionProvider>();
        var repeatedTransaction = firstScope.ServiceProvider.GetRequiredService<IAmABoxTransactionProvider>();
        var secondTransaction = secondScope.ServiceProvider.GetRequiredService<IAmABoxTransactionProvider>();
        var firstConnection = firstScope.ServiceProvider.GetRequiredService<IAmARelationalDbConnectionProvider>();
        var repeatedConnection = firstScope.ServiceProvider.GetRequiredService<IAmARelationalDbConnectionProvider>();
        var secondConnection = secondScope.ServiceProvider.GetRequiredService<IAmARelationalDbConnectionProvider>();
        await Assert.That(firstTransaction).IsTypeOf<SqliteTransactionProvider>();
        await Assert.That(firstConnection).IsTypeOf<SqliteConnectionProvider>();

        if (lifetime == ServiceLifetime.Transient)
        {
            await Assert.That(repeatedTransaction).IsNotSameReferenceAs(firstTransaction);
            await Assert.That(repeatedConnection).IsNotSameReferenceAs(firstConnection);
        }
        else
        {
            await Assert.That(repeatedTransaction).IsSameReferenceAs(firstTransaction);
            await Assert.That(repeatedConnection).IsSameReferenceAs(firstConnection);
        }

        if (lifetime == ServiceLifetime.Singleton)
        {
            await Assert.That(secondTransaction).IsSameReferenceAs(firstTransaction);
            await Assert.That(secondConnection).IsSameReferenceAs(firstConnection);
        }
        else
        {
            await Assert.That(secondTransaction).IsNotSameReferenceAs(firstTransaction);
            await Assert.That(secondConnection).IsNotSameReferenceAs(firstConnection);
        }
    }
}
