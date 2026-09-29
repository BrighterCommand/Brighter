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

namespace Paramore.Brighter.Extensions.Tests.RelationalConfiguration;

public class ScopedRelationalConfigurationTests
{
    [Test]
    public async Task When_registering_scoped_configuration_first_should_preserve_scope_identity()
    {
        //Arrange
        var outboxConfiguration = new RelationalDatabaseConfiguration("Data Source=:memory:");
        var services = new ServiceCollection();
        var factoryCalls = 0;
        services.AddScoped<IAmARelationalDatabaseConfiguration>(_ =>
        {
            factoryCalls++;
            return new RelationalDatabaseConfiguration("Data Source=:memory:", outBoxTableName: "ScopedOutbox");
        });
        services.AddBrighter().AddProducers(options =>
        {
            options.Outbox = new SqliteOutbox(outboxConfiguration);
            options.TransactionProvider = typeof(SqliteTransactionProvider);
            options.ConnectionProvider = typeof(SqliteConnectionProvider);
        }, ServiceLifetime.Scoped);
        await Assert.That(factoryCalls).IsEqualTo(0);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        //Act
        var first = firstScope.ServiceProvider.GetRequiredService<IAmARelationalDatabaseConfiguration>();
        var repeated = firstScope.ServiceProvider.GetRequiredService<IAmARelationalDatabaseConfiguration>();
        var second = secondScope.ServiceProvider.GetRequiredService<IAmARelationalDatabaseConfiguration>();

        //Assert
        await Assert.That(first).IsNotSameReferenceAs(outboxConfiguration);
        await Assert.That(repeated).IsSameReferenceAs(first);
        await Assert.That(second).IsNotSameReferenceAs(first);
        await Assert.That(factoryCalls).IsEqualTo(2);
        await Assert.That(firstScope.ServiceProvider.GetRequiredService<IAmABoxTransactionProvider>()).IsTypeOf<SqliteTransactionProvider>();
        await Assert.That(firstScope.ServiceProvider.GetRequiredService<IAmARelationalDbConnectionProvider>()).IsTypeOf<SqliteConnectionProvider>();
        await Assert.That(secondScope.ServiceProvider.GetRequiredService<IAmABoxTransactionProvider>()).IsTypeOf<SqliteTransactionProvider>();
        await Assert.That(secondScope.ServiceProvider.GetRequiredService<IAmARelationalDbConnectionProvider>()).IsTypeOf<SqliteConnectionProvider>();
        await Assert.That(factoryCalls).IsEqualTo(2);
    }
}
