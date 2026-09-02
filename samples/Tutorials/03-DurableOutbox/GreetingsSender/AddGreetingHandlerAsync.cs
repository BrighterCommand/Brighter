#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Greetings;
using Paramore.Brighter;

namespace GreetingsSender;

/// <summary>
/// The whole point of rung 3. The business row and the outgoing message are written on the
/// same connection inside the same transaction, so the database commits both or neither.
/// </summary>
public class AddGreetingHandlerAsync : RequestHandlerAsync<AddGreeting>
{
    private readonly IAmATransactionConnectionProvider _transactionProvider;
    private readonly IAmACommandProcessor _postBox;

    public AddGreetingHandlerAsync(
        IAmATransactionConnectionProvider transactionProvider,
        IAmACommandProcessor postBox)
    {
        _transactionProvider = transactionProvider;
        _postBox = postBox;
    }

    public override async Task<AddGreeting> HandleAsync(
        AddGreeting addGreeting,
        CancellationToken cancellationToken = default)
    {
        // Ask the provider for the connection and transaction rather than opening your own.
        // This is the shared unit of work: the Outbox writes through the same pair, which is
        // what makes the two writes below one atomic act instead of two hopeful ones.
        DbConnection connection = await _transactionProvider.GetConnectionAsync(cancellationToken);
        DbTransaction transaction = await _transactionProvider.GetTransactionAsync(cancellationToken);

        try
        {
            // 1. Your write, to your table. Brighter neither knows nor cares what this is.
            await using (DbCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "insert into Greeting (Message) values (@message)";

                DbParameter message = command.CreateParameter();
                message.ParameterName = "message";
                message.Value = addGreeting.Greeting;
                command.Parameters.Add(message);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            // 2. Brighter's write, to the Outbox table, on that same transaction. Nothing has
            // gone to RabbitMQ yet — DepositPostAsync only stores the message.
            await _postBox.DepositPostAsync(
                new GreetingEvent(addGreeting.Greeting),
                _transactionProvider,
                cancellationToken: cancellationToken);

            // The deliberate failure the tutorial's last step runs. Both writes are pending
            // right now; neither is committed. Throwing here is the experiment.
            if (addGreeting.FailBeforeCommit)
            {
                throw new InvalidOperationException(
                    "Deliberate failure, after both writes and before the commit");
            }

            // 3. Both, or neither.
            await _transactionProvider.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Rolling back discards your row and the message together. There is no window in
            // which the greeting exists but the message does not, or the other way round.
            await _transactionProvider.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            _transactionProvider.Close();
        }

        // Note what is NOT here: ClearOutboxAsync. The message sits in the Outbox until the
        // Sweeper picks it up, which is the delay this rung exists to show you. Call
        // ClearOutboxAsync here instead and it dispatches immediately, at the cost of doing
        // the send on the request thread.
        return await base.HandleAsync(addGreeting, cancellationToken);
    }
}
