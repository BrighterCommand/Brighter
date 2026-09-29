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

public class LaterRelationalConfigurationTests
{
    [Test]
    [Arguments(ServiceLifetime.Singleton)]
    [Arguments(ServiceLifetime.Scoped)]
    public async Task When_registering_configuration_after_producers_should_override_the_fallback(ServiceLifetime lifetime)
    {
        //Arrange
        var outboxConfiguration = new RelationalDatabaseConfiguration("Data Source=:memory:");
        var explicitConfiguration = new RelationalDatabaseConfiguration("Data Source=:memory:", outBoxTableName: "ExplicitOutbox");
        var services = new ServiceCollection();
        services.AddBrighter().AddProducers(options =>
        {
            options.Outbox = new SqliteOutbox(outboxConfiguration);
            options.TransactionProvider = typeof(SqliteTransactionProvider);
            options.ConnectionProvider = typeof(SqliteConnectionProvider);
        }, ServiceLifetime.Scoped);
        await Assert.That(services.Any(descriptor => descriptor.ServiceType == typeof(IAmARelationalDatabaseConfiguration))).IsTrue();
        if (lifetime == ServiceLifetime.Singleton)
            services.AddSingleton<IAmARelationalDatabaseConfiguration>(explicitConfiguration);
        else
            services.AddScoped<IAmARelationalDatabaseConfiguration>(_ => explicitConfiguration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        //Act
        var configuration = scope.ServiceProvider.GetRequiredService<IAmARelationalDatabaseConfiguration>();

        //Assert
        await Assert.That(configuration).IsSameReferenceAs(explicitConfiguration);
        await Assert.That(scope.ServiceProvider.GetRequiredService<IAmABoxTransactionProvider>()).IsTypeOf<SqliteTransactionProvider>();
        await Assert.That(scope.ServiceProvider.GetRequiredService<IAmARelationalDbConnectionProvider>()).IsTypeOf<SqliteConnectionProvider>();
    }
}
