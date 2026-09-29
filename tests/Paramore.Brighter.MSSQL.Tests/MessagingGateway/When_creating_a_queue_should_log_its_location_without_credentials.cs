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

#nullable enable

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.MsSql.SqlQueues;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.MSSQL.Tests.MessagingGateway.TestDoubles;
using System.Threading.Tasks;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

public class MsSqlMessageQueueLoggingTests
{
    [Test]
    public async Task When_creating_a_queue_should_log_its_location_without_credentials()
    {
        //Arrange
        var logs = new InMemoryQueueLogCapture();
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Debug).AddProvider(logs));
        var previousLoggerFactory = ApplicationLogging.LoggerFactory;
        ApplicationLogging.LoggerFactory = loggerFactory;

        try
        {
            // This closed generic has its own static logger, independent of other queue tests.
            RuntimeHelpers.RunClassConstructor(typeof(MsSqlMessageQueue<MsSqlMessageQueueLoggingTests>).TypeHandle);

            string[] connectionStrings =
            [
                "Server=queue-server;Database=QueueDatabase;User ID=queue-user;Password=\"test;password=secret\"",
                "Data Source=queue-server;Initial Catalog=QueueDatabase;UID=queue-user;Pwd=\"test;password=secret\"",
                "Server=queue-server;Database=QueueDatabase;Integrated Security=true"
            ];

            foreach (var connectionString in connectionStrings)
            {
                logs.Entries.Clear();
                var configuration = new RelationalDatabaseConfiguration(connectionString,
                    queueStoreTable: "QueueMessages");

                //Act
                _ = new MsSqlMessageQueue<MsSqlMessageQueueLoggingTests>(
                    configuration, new MsSqlConnectionProvider(configuration));

                //Assert
                var entry = await Assert.That(logs.Entries).HasSingleItem();
                await Assert.That(entry.Level).IsEqualTo(LogLevel.Debug);
                await Assert.That(entry.Message).DoesNotContain("test;password=secret");
                await Assert.That(entry.Message).DoesNotContain("queue-user");
                await Assert.That(entry.Message).DoesNotContain(connectionString);
                await Assert.That(entry.Message).Contains("queue-server");
                await Assert.That(entry.Message).Contains("QueueDatabase");
                await Assert.That(entry.Message).Contains("QueueMessages");
                await Assert.That(entry.Properties["DataSource"]).IsEqualTo("queue-server");
                await Assert.That(entry.Properties["InitialCatalog"]).IsEqualTo("QueueDatabase");
                await Assert.That(entry.Properties["QueueStoreTable"]).IsEqualTo("QueueMessages");
                await Assert.That(entry.Properties.Keys).DoesNotContain("ConnectionString");
                using (Assert.Multiple())
                {
                    foreach (var value in entry.Properties.Values)
                    {
                        var text = value?.ToString() ?? string.Empty;
                    }
                }
            }
        }
        finally
        {
            ApplicationLogging.LoggerFactory = previousLoggerFactory;
        }
    }
}
