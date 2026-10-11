using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// An <see cref="IServiceScopeFactory"/> whose first <see cref="CreateScope"/> throws, as a
/// provider being torn down at shutdown would; later calls go to the real factory.
/// </summary>
public sealed class FailsFirstScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
{
    private int _calls;

    public IServiceScope CreateScope() =>
        Interlocked.Increment(ref _calls) == 1
            ? throw new ObjectDisposedException(nameof(IServiceProvider))
            : inner.CreateScope();
}
