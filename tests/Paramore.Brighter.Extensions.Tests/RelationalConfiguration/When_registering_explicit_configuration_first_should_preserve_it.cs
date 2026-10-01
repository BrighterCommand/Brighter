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

public class ExistingRelationalConfigurationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_registering_explicit_configuration_first_should_preserve_it(bool useFactory)
    {
        //Arrange
        var outboxConfiguration = new RelationalDatabaseConfiguration("Data Source=:memory:", outBoxTableName: "Outbox");
        var explicitConfiguration = new RelationalDatabaseConfiguration("Data Source=:memory:", outBoxTableName: "ExplicitOutbox");
        var services = new ServiceCollection();
        services.AddSingleton(Initializer.Factory);
        var factoryCalls = 0;
        if (useFactory)
        {
            services.AddSingleton<IAmARelationalDatabaseConfiguration>(_ =>
            {
                factoryCalls++;
                return explicitConfiguration;
            });
        }
        else
        {
            services.AddSingleton<IAmARelationalDatabaseConfiguration>(explicitConfiguration);
        }

        services.AddBrighter().AddProducers(options =>
        {
            options.Outbox = new SqliteOutbox(outboxConfiguration, logger: Microsoft.Extensions.Logging.Abstractions.NullLogger<SqliteOutbox>.Instance);
            options.TransactionProvider = typeof(SqliteTransactionProvider);
            options.ConnectionProvider = typeof(SqliteConnectionProvider);
        });
        Assert.Equal(0, factoryCalls);
        using var provider = services.BuildServiceProvider();

        //Act
        var configuration = provider.GetRequiredService<IAmARelationalDatabaseConfiguration>();

        //Assert
        Assert.Same(explicitConfiguration, configuration);
        Assert.Same(explicitConfiguration, Assert.Single(provider.GetServices<IAmARelationalDatabaseConfiguration>()));
        Assert.Equal(useFactory ? 1 : 0, factoryCalls);
        Assert.IsType<SqliteTransactionProvider>(provider.GetRequiredService<IAmABoxTransactionProvider>());
        Assert.IsType<SqliteConnectionProvider>(provider.GetRequiredService<IAmARelationalDbConnectionProvider>());
    }
}
