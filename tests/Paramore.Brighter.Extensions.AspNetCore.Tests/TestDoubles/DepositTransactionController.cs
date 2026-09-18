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
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Opens a transaction on its own <see cref="DepositTransactionDbContext"/>, writes a
/// <see cref="DepositedEntity"/>, then <c>Send</c>s a <see cref="DepositEntityCommand"/> whose handler
/// deposits a message into the outbox over the same context, before committing or rolling back
/// depending on the <c>rollback</c> query flag (AC-52).
/// </summary>
[ApiController]
[Route("api/deposit-transactions")]
public sealed class DepositTransactionController : ControllerBase
{
    private const string EntityName = "an-entity";

    private readonly IAmACommandProcessor _commandProcessor;
    private readonly DepositTransactionDbContext _dbContext;
    private readonly DepositTransactionRecorder _recorder;

    public DepositTransactionController(
        IAmACommandProcessor commandProcessor,
        DepositTransactionDbContext dbContext,
        DepositTransactionRecorder recorder)
    {
        _commandProcessor = commandProcessor;
        _dbContext = dbContext;
        _recorder = recorder;
    }

    [HttpPost]
    public IActionResult Deposit([FromQuery] bool rollback)
    {
        _recorder.RecordController(_dbContext);

        using var transaction = _dbContext.Database.BeginTransaction();

        _dbContext.DepositedEntities.Add(new DepositedEntity { Name = EntityName });
        _dbContext.SaveChanges();

        _commandProcessor.Send(new DepositEntityCommand());

        _recorder.RecordMidTransactionVisibility(
            entityVisible: _dbContext.DepositedEntities.AsNoTracking().Any(e => e.Name == EntityName),
            outboxRowVisible: CountOutboxRowsOnSameConnection(transaction) > 0);

        if (rollback)
            transaction.Rollback();
        else
            transaction.Commit();

        return Ok();
    }

    /// <summary>
    /// Counts outbox rows over this controller's own connection - meaningful only when that connection
    /// has the outbox's database attached (AC-52's committing/rollback facts, where the handler shares
    /// this <see cref="DepositTransactionDbContext"/>). Under <see cref="ScopeAffinity.AlwaysNew"/>, the
    /// handler's own, separate connection is deliberately the only one ever attached to it (see
    /// <see cref="OutboxDatabaseAttachment"/>), so this controller's own connection never sees it - zero,
    /// correctly, without needing to attach it here too.
    /// </summary>
    private int CountOutboxRowsOnSameConnection(IDbContextTransaction transaction)
    {
        var connection = _dbContext.Database.GetDbConnection();

        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.Transaction = transaction.GetDbTransaction();
            checkCommand.CommandText = "SELECT COUNT(*) FROM pragma_database_list WHERE name = 'outboxdb'";
            if (Convert.ToInt64(checkCommand.ExecuteScalar()) == 0)
                return 0;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = $"SELECT COUNT(*) FROM {DepositTransactionWebApplicationFactory.AttachedOutboxTableReference}";
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
