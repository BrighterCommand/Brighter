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

using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.MessagingGateway.MsSql.SqlQueues;

public partial class MsSqlMessageQueue<T>
{
    private const int PURGE_BATCH_SIZE = 500;

    internal void SendToChannels(T message, RoutingKey logicalTopic, ChannelName[] channels, int timeoutSeconds)
    {
        using var connection = _connectionProvider.GetConnection();
        using var command = FanOutCommand(connection, message, logicalTopic, channels, timeoutSeconds);
        try
        {
            command.ExecuteNonQuery();
        }
        catch (SqlException exception) when (exception.Number == 51001)
        {
            throw new ConfigurationException("An MS SQL subscriber channel must differ from its logical topic under the queue column's collation.", exception);
        }
    }

    internal async Task SendToChannelsAsync(T message, RoutingKey logicalTopic, ChannelName[] channels, int timeoutSeconds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        using var command = FanOutCommand(connection, message, logicalTopic, channels, timeoutSeconds);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }
        catch (SqlException exception) when (exception.Number == 51001)
        {
            throw new ConfigurationException("An MS SQL subscriber channel must differ from its logical topic under the queue column's collation.", exception);
        }
    }

    internal void PurgeTopic(string topic)
    {
        using var connection = _connectionProvider.GetConnection();
        using var command = PurgeTopicCommand(connection, topic);
        command.ExecuteNonQuery();
    }

    internal async Task PurgeTopicAsync(string topic, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = await _connectionProvider.GetConnectionAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        using var command = PurgeTopicCommand(connection, topic);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
    }

    // The empty table projection makes UNION compare names using the Topic column's collation.
    // One INSERT keeps delivery atomic even when one destination fails a table constraint.
    private DbCommand FanOutCommand(DbConnection connection, T message, RoutingKey logicalTopic, ChannelName[] channels, int timeoutSeconds)
    {
        var table = _configuration.QueueStoreTable.Replace("]", "]]");
        var parameters = channels.Select((channel, index) =>
            new SqlParameter($"@channel{index}", SqlDbType.NVarChar, 255) { Value = channel.Value }).ToArray();
        var values = string.Join(", ", parameters.Select(parameter => $"({parameter.ParameterName})"));
        var command = connection.CreateCommand();
        command.CommandTimeout = timeoutSeconds;
        command.CommandText = $"""
            SET NOCOUNT ON;
            IF EXISTS (
                SELECT 1 FROM (
                    SELECT Topic FROM [{table}] WHERE 1 = 0
                    UNION
                    SELECT Topic FROM (VALUES {values}) AS recipients(Topic)
                ) AS destinations
                WHERE Topic = @logicalTopic
            )
                THROW 51001, 'An MS SQL subscriber channel must differ from its logical topic.', 1;
            INSERT INTO [{table}] (Topic, MessageType, Payload)
            SELECT Topic, @messageType, @payload
            FROM (
                SELECT Topic FROM [{table}] WHERE 1 = 0
                UNION
                SELECT Topic FROM (VALUES {values}) AS recipients(Topic)
            ) AS destinations;
            """;
        command.Parameters.AddRange(parameters);
        command.Parameters.Add(new SqlParameter("@logicalTopic", SqlDbType.NVarChar, -1) { Value = logicalTopic.Value });
        command.Parameters.Add(new SqlParameter("@messageType", SqlDbType.NVarChar, 1024)
        { Value = typeof(T).FullName ?? throw new InvalidOperationException("The message type must have a full name.") });
        command.Parameters.Add(new SqlParameter("@payload", SqlDbType.NVarChar, -1)
        { Value = JsonSerializer.Serialize(message, JsonSerialisationOptions.Options) });
        return command;
    }

    private DbCommand PurgeTopicCommand(DbConnection connection, string topic)
    {
        var table = _configuration.QueueStoreTable.Replace("]", "]]");
        var command = connection.CreateCommand();
        command.CommandText = $"""
            SET NOCOUNT ON;
            DECLARE @maxId bigint = (SELECT MAX(Id) FROM [{table}] WHERE Topic = @topic);
            WHILE @maxId IS NOT NULL
            BEGIN
                DELETE TOP ({PURGE_BATCH_SIZE}) FROM [{table}] WITH (ROWLOCK)
                WHERE Topic = @topic AND Id <= @maxId;
                IF @@ROWCOUNT = 0 BREAK;
            END;
            """;
        command.Parameters.Add(CreateDbDataParameter("@topic", topic));
        return command;
    }
}
