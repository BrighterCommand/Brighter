#region Sources
//copy of Stephen Cleary's Nito Disposables SingleDisposable
//see https://github.com/StephenCleary/Disposables/blob/main/src/Nito.Disposables/SingleDisposable.cs
#endregion

using System;
using System.Threading.Tasks;

namespace Paramore.Brighter.Tasks;

internal abstract class SingleDisposable<T> : IDisposable
{
    private readonly BoundActionField<T> _context;

    private readonly TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>();

    protected SingleDisposable(T context)
    {
        _context = new BoundActionField<T>(Dispose, context);
    }

    private bool IsDisposeStarted => _context.IsEmpty;

    private bool IsDisposed => _tcs.Task.IsCompleted;

    public bool IsDisposing => IsDisposeStarted && !IsDisposed;

    protected abstract void Dispose(T context);

    public void Dispose()
    {
        var context = _context.TryGetAndUnset();
        if (context == null)
        {
            _tcs.Task.GetAwaiter().GetResult();
            return;
        }

        try
        {
            context.Invoke();
        }
        finally
        {
            _tcs.TrySetResult(null!);
        }
    }

    protected bool TryUpdateContext(Func<T, T> contextUpdater) => _context.TryUpdateContext(contextUpdater);
}
