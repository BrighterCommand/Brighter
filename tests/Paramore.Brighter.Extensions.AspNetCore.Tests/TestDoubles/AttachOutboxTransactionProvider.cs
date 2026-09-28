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

using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Paramore.Brighter.Sqlite.EntityFrameworkCore;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// The configured transaction provider for <see cref="DepositTransactionDbContext"/> (AC-52), which
/// additionally attaches the outbox's own, physically separate database file to whichever connection this
/// provider's context ends up using - however and whenever that connection actually gets opened.
/// </summary>
/// <remarks>
/// <see cref="SqliteEntityFrameworkTransactionProvider{T}.GetConnection"/> calls
/// <c>context.Database.CanConnect()</c>, which leaves the connection closed again, and Brighter's own
/// outbox write path opens it afterwards with a plain <c>connection.Open()</c> call that never goes
/// through Entity Framework Core's own connection-interception pipeline - so attaching inside an override
/// of <see cref="GetConnection"/> here would run before the connection is actually open. Subscribing to
/// the connection's own <see cref="DbConnection.StateChange"/> event once, at construction, reacts to
/// every future open regardless of which code path performed it. Under <see cref="ScopeAffinity.JoinAmbient"/>
/// the caller (e.g. a controller) has typically already opened its own transaction - and therefore its
/// connection - before this provider is even constructed, so subscribing alone would miss that connection's
/// one and only open transition; attaching immediately, in the constructor, if the connection is already
/// open covers that case too.
/// <para>
/// Because this provider is constructed once per pipeline (one instance per <c>Send</c>/<c>Post</c>,
/// scoped to whichever <see cref="DepositTransactionDbContext"/> that pipeline resolved), only a
/// connection this provider's own context actually uses gets attached - never the caller's own, separate
/// connection under <see cref="ScopeAffinity.AlwaysNew"/>, which is the whole point: see
/// <see cref="OutboxDatabaseAttachment"/>.
/// </para>
/// </remarks>
public sealed class AttachOutboxTransactionProvider : SqliteEntityFrameworkTransactionProvider<DepositTransactionDbContext>
{
    public AttachOutboxTransactionProvider(DepositTransactionDbContext context, OutboxDatabaseLocation outboxDatabaseLocation)
        : base(context)
    {
        var connection = context.Database.GetDbConnection();

        connection.StateChange += (sender, args) =>
        {
            if (args.CurrentState != ConnectionState.Open)
                return;

            OutboxDatabaseAttachment.EnsureAttached((DbConnection)sender!, outboxDatabaseLocation.Path);
        };

        if (connection.State == ConnectionState.Open)
            OutboxDatabaseAttachment.EnsureAttached(connection, outboxDatabaseLocation.Path);
    }
}
