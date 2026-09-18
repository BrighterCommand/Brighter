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

using System.Data.Common;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// A sync handler for <see cref="DepositEntityCommand"/> (AC-52) that injects both the shared
/// <see cref="DepositTransactionDbContext"/> and an <see cref="IAmABoxTransactionProvider{DbTransaction}"/>
/// over it, and deposits <see cref="DepositEntityPostedCommand"/> into the outbox using that provider -
/// joining whatever transaction is already open on the shared context, without committing or rolling
/// back anything itself.
/// </summary>
public sealed class DepositEntityCommandHandler : RequestHandler<DepositEntityCommand>
{
    private readonly IAmACommandProcessor _postBox;
    private readonly IAmABoxTransactionProvider<DbTransaction> _transactionProvider;

    public DepositEntityCommandHandler(
        DepositTransactionDbContext dbContext,
        IAmABoxTransactionProvider<DbTransaction> transactionProvider,
        IAmACommandProcessor postBox,
        DepositTransactionRecorder recorder)
    {
        _postBox = postBox;
        _transactionProvider = transactionProvider;
        recorder.RecordHandler(dbContext);
    }

    public override DepositEntityCommand Handle(DepositEntityCommand command)
    {
        _postBox.DepositPost(new DepositEntityPostedCommand(), _transactionProvider);
        return base.Handle(command);
    }
}
