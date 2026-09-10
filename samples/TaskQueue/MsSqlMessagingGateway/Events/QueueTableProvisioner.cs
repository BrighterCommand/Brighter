#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Microsoft.Data.SqlClient;
using Paramore.Brighter.MessagingGateway.MsSql;

namespace Events;

/// <summary>
/// Creates the queue table if it is missing.
/// </summary>
/// <remarks>
/// Box Provisioning covers the Outbox and the Inbox, but nothing covers the queue: the MSSQL
/// gateway has no provisioning path at all, so <c>OnMissingChannel.Create</c> is accepted on a
/// publication or subscription and then never acted on. Without this, the first thing either
/// application does is fail with <c>Invalid object name 'QueueData'</c> from inside the pump.
///
/// The DDL is Brighter's own, from <see cref="MsSqlQueueBuilder"/> — which is public precisely
/// because callers have to run it themselves.
/// </remarks>
public static class QueueTableProvisioner
{
    public static void EnsureQueueTable(string connectionString, string queueTableName)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using (var exists = connection.CreateCommand())
        {
            exists.CommandText = MsSqlQueueBuilder.GetExistsQuery(queueTableName);
            if (Convert.ToInt32(exists.ExecuteScalar()) == 1) return;
        }

        using (var create = connection.CreateCommand())
        {
            create.CommandText = MsSqlQueueBuilder.GetDDL(queueTableName);
            create.ExecuteNonQuery();
        }

        using (var index = connection.CreateCommand())
        {
            index.CommandText = MsSqlQueueBuilder.GetIndexDDL(queueTableName);
            index.ExecuteNonQuery();
        }

        Console.WriteLine($"Created queue table '{queueTableName}'");
    }
}
