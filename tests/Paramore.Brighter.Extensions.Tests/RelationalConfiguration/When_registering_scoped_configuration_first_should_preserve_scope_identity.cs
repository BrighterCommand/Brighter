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

public class ScopedRelationalConfigurationTests
{
    [Fact]
    public void When_registering_scoped_configuration_first_should_preserve_scope_identity()
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
        Assert.Equal(0, factoryCalls);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        //Act
        var first = firstScope.ServiceProvider.GetRequiredService<IAmARelationalDatabaseConfiguration>();
        var repeated = firstScope.ServiceProvider.GetRequiredService<IAmARelationalDatabaseConfiguration>();
        var second = secondScope.ServiceProvider.GetRequiredService<IAmARelationalDatabaseConfiguration>();

        //Assert
        Assert.NotSame(outboxConfiguration, first);
        Assert.Same(first, repeated);
        Assert.NotSame(first, second);
        Assert.Equal(2, factoryCalls);
        Assert.IsType<SqliteTransactionProvider>(firstScope.ServiceProvider.GetRequiredService<IAmABoxTransactionProvider>());
        Assert.IsType<SqliteConnectionProvider>(firstScope.ServiceProvider.GetRequiredService<IAmARelationalDbConnectionProvider>());
        Assert.IsType<SqliteTransactionProvider>(secondScope.ServiceProvider.GetRequiredService<IAmABoxTransactionProvider>());
        Assert.IsType<SqliteConnectionProvider>(secondScope.ServiceProvider.GetRequiredService<IAmARelationalDbConnectionProvider>());
        Assert.Equal(2, factoryCalls);
    }
}
