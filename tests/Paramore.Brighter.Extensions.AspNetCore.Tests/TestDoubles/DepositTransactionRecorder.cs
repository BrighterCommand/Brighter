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

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Records what <see cref="DepositTransactionController"/> and <see cref="DepositEntityCommandHandler"/>
/// each observed during one request (AC-52), so a test can compare the two <see cref="DepositTransactionDbContext"/>
/// instances they resolved, and what was visible mid-transaction, without reaching into the request scope
/// itself. Register as a singleton in the container under test.
/// </summary>
public sealed class DepositTransactionRecorder
{
    private DepositTransactionDbContext? _controllerDbContext;
    private DepositTransactionDbContext? _handlerDbContext;

    /// <summary>
    /// Records the <see cref="DepositTransactionDbContext"/> the controller resolved.
    /// </summary>
    public void RecordController(DepositTransactionDbContext dbContext) => _controllerDbContext = dbContext;

    /// <summary>
    /// Records the <see cref="DepositTransactionDbContext"/> the handler resolved.
    /// </summary>
    public void RecordHandler(DepositTransactionDbContext dbContext) => _handlerDbContext = dbContext;

    /// <summary>
    /// Records what the controller observed, on its own <see cref="DepositTransactionDbContext"/>, in the
    /// still-open transaction, immediately after <c>Send</c> returned and before commit or rollback.
    /// </summary>
    public void RecordMidTransactionVisibility(bool entityVisible, bool outboxRowVisible)
    {
        EntityVisibleMidTransaction = entityVisible;
        OutboxRowVisibleMidTransaction = outboxRowVisible;
    }

    /// <summary>
    /// Whether the handler resolved the exact same <see cref="DepositTransactionDbContext"/> instance as
    /// the controller.
    /// </summary>
    public bool HandlerSharesControllerDbContext => ReferenceEquals(_controllerDbContext, _handlerDbContext);

    /// <summary>
    /// Whether the entity the controller added was visible to a fresh query on its own
    /// <see cref="DepositTransactionDbContext"/>, before commit or rollback.
    /// </summary>
    public bool EntityVisibleMidTransaction { get; private set; }

    /// <summary>
    /// Whether the outbox row the handler deposited was visible on the controller's own, still-open
    /// transaction, before commit or rollback.
    /// </summary>
    public bool OutboxRowVisibleMidTransaction { get; private set; }
}
