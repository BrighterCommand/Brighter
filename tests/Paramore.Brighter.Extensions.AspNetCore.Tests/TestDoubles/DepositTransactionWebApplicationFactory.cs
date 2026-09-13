#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.Sqlite.EntityFrameworkCore;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// An ASP.NET test host (AC-52's setup) with a real, file-backed Sqlite database: a <c>Scoped</c>
/// <see cref="DepositTransactionDbContext"/> registered <c>AddDbContext</c>, a relational
/// <see cref="SqliteOutbox"/>, and an <see cref="AttachOutboxTransactionProvider"/> (a
/// <see cref="SqliteEntityFrameworkTransactionProvider{T}"/> over that context) as the configured
/// transaction provider. Parameterised by the affinity passed to
/// <see cref="BrighterAspNetCoreExtensions.AddBrighterRequestScope"/>, so a test can compare an opted-in
/// host against an <see cref="ScopeAffinity.AlwaysNew"/> negative control. Each instance gets its own two,
/// uniquely named database files (entity table and outbox - see <see cref="OutboxDatabaseAttachment"/>),
/// deleted when the factory is disposed.
/// </summary>
public sealed class DepositTransactionWebApplicationFactory : WebApplicationFactory<DepositTransactionController>
{
    /// <summary>
    /// The outbox's own table name, as it is physically named inside its own database file.
    /// </summary>
    public const string OutboxTableName = "DepositTransactionOutbox";

    /// <summary>
    /// How <see cref="OutboxTableName"/> is referenced in SQL executed over a connection that has the
    /// outbox's database file attached (every connection Brighter's outbox mechanism actually uses) -
    /// see <see cref="OutboxDatabaseAttachment"/>.
    /// </summary>
    public const string AttachedOutboxTableReference = "outboxdb." + OutboxTableName;

    private readonly ScopeAffinity _affinity;
    private readonly string _databasePath;
    private readonly string _outboxDatabasePath;

    /// <summary>
    /// The connection string for this factory's own entity database file - so a test can open its own,
    /// separate connection once the HTTP round trip has completed, to inspect what was actually
    /// persisted.
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// The connection string for this factory's own, physically separate outbox database file - see
    /// <see cref="OutboxDatabaseAttachment"/> for why the outbox is never in the same file as the entity
    /// table.
    /// </summary>
    public string OutboxConnectionString { get; }

    /// <param name="affinity">The affinity passed to
    /// <see cref="BrighterAspNetCoreExtensions.AddBrighterRequestScope"/>.</param>
    public DepositTransactionWebApplicationFactory(ScopeAffinity affinity)
    {
        _affinity = affinity;
        _databasePath = Path.Combine(Path.GetTempPath(), $"deposit-transaction-{Guid.NewGuid():N}.db");
        _outboxDatabasePath = Path.Combine(Path.GetTempPath(), $"deposit-transaction-outbox-{Guid.NewGuid():N}.db");
        ConnectionString = $"Data Source={_databasePath}";
        OutboxConnectionString = $"Data Source={_outboxDatabasePath}";

        CreateSchema();
    }

    private void CreateSchema()
    {
        using (var connection = new SqliteConnection(ConnectionString))
        {
            connection.Open();
            using var entityCommand = connection.CreateCommand();
            entityCommand.CommandText =
                "CREATE TABLE DepositedEntities (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL)";
            entityCommand.ExecuteNonQuery();
        }

        using (var outboxConnection = new SqliteConnection(OutboxConnectionString))
        {
            outboxConnection.Open();
            using var outboxCommand = outboxConnection.CreateCommand();
            outboxCommand.CommandText = SqliteOutboxBuilder.GetDDL(OutboxTableName);
            outboxCommand.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Pins the content root to the test assembly's own output directory, mirroring
    /// <see cref="PlaceOrderWebApplicationFactory"/> - the test assembly is the entry point, so the base
    /// class's own content-root discovery resolves to a path that does not exist.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseContentRoot(AppContext.BaseDirectory);
        return base.CreateHost(builder);
    }

    /// <inheritdoc />
    protected override IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                ConfigureWebHost(webBuilder);
            });
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // SqliteOutbox resolves ApplicationLogging.LoggerFactory (a process-wide mutable static)
            // eagerly, in its own constructor, every time one is constructed - not once, cached, like a
            // closed generic Brighter type's own static logger field. Constructing one directly below,
            // during ConfigureServices and therefore before this host's own CommandProcessor has had a
            // chance to repin that static to itself, is vulnerable to whatever value a concurrently
            // running test's already-disposed host last left there. Re-pinning it to Initializer's own,
            // never-disposed factory immediately before that construction closes that window.
            ApplicationLogging.LoggerFactory = Initializer.Factory;

            services.AddControllers().AddApplicationPart(typeof(DepositTransactionController).Assembly);

            services.AddDbContext<DepositTransactionDbContext>(options => options.UseSqlite(ConnectionString));
            services.AddSingleton<DepositTransactionRecorder>();
            services.AddSingleton(new OutboxDatabaseLocation(_outboxDatabasePath));

            var capturingLoggerProvider = new CapturingLoggerProvider();
            services.AddSingleton(capturingLoggerProvider);
            services.AddLogging(logging => logging.AddProvider(capturingLoggerProvider));

            var routingKey = new RoutingKey("deposit-transaction-posted");
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { routingKey, new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = routingKey, RequestType = typeof(DepositEntityPostedCommand) }) }
            });

            var outboxConfiguration = new RelationalDatabaseConfiguration(
                connectionString: ConnectionString,
                databaseName: "deposit-transaction",
                outBoxTableName: AttachedOutboxTableReference,
                binaryMessagePayload: false);

            services.AddBrighterRequestScope(_affinity);
            services.AddBrighter(options =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.MapperLifetime = ServiceLifetime.Scoped;
                options.TransformerLifetime = ServiceLifetime.Scoped;
            })
            .AddProducers(cfg =>
            {
                cfg.ProducerRegistry = producerRegistry;
                cfg.Outbox = new SqliteOutbox(outboxConfiguration, new AttachOutboxConnectionProvider(outboxConfiguration, _outboxDatabasePath));
                cfg.TransactionProvider = typeof(AttachOutboxTransactionProvider);
            }, ServiceLifetime.Scoped);
        });

        builder.Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        });
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        foreach (var path in new[] { _databasePath, _outboxDatabasePath })
        {
            if (!File.Exists(path))
                continue;

            try { File.Delete(path); } catch (IOException) { /* best-effort cleanup */ }
        }
    }
}
