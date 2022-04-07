using System;
using System.Threading;
using System.Threading.Tasks;
using Marten;

namespace GreetingsWeb.Extensions
{
    public class MartenRetryPolicy : IRetryPolicy
    {
        private readonly int maxTries;
        private readonly Func<Exception, bool> filter;

        private MartenRetryPolicy(int maxTries, Func<Exception, bool> filter)
        {
            this.maxTries = maxTries;
            this.filter = filter;
        }

        public static IRetryPolicy Twice(Func<Exception, bool> filter = null)
        {
            return new MartenRetryPolicy(3, filter ?? (_ => true));
        }

        public void Execute(Action operation)
        {
            Try(() => { operation(); return Task.CompletedTask; }, CancellationToken.None).GetAwaiter().GetResult();
        }

        public TResult Execute<TResult>(Func<TResult> operation)
        {
            return Try(() => Task.FromResult(operation()), CancellationToken.None).GetAwaiter().GetResult();
        }

        public Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken)
        {
            return Try(operation, cancellationToken);
        }

        public Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken)
        {
            return Try(operation, cancellationToken);
        }

        private async Task Try(Func<Task> operation, CancellationToken token)
        {
            for (var tries = 0; ; token.ThrowIfCancellationRequested())
            {
                try
                {
                    await operation();
                    return;
                }
                catch (Exception e) when (++tries < maxTries && filter(e))
                {
                }
            }
        }

        private async Task<T> Try<T>(Func<Task<T>> operation, CancellationToken token)
        {
            for (var tries = 0; ; token.ThrowIfCancellationRequested())
            {
                try
                {
                    return await operation();
                }
                catch (Exception e) when (++tries < maxTries && filter(e))
                {
                }
            }
        }
    }
}
