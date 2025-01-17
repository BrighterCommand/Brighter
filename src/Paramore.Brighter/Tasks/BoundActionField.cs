#region Sources
//copy of Stephen Cleary's Nito Disposables BoundAction.cs
// see https://github.com/StephenCleary/Disposables/blob/main/src/Nito.Disposables/Internals/BoundAction.cs
#endregion

using System;
using System.Threading;

namespace Paramore.Brighter.Tasks;

internal sealed class BoundActionField<T>(Action<T> action, T context)
{
        private BoundAction? _field = new(action, context);

        public bool IsEmpty => Interlocked.CompareExchange(ref _field, null, null) == null;

        public IBoundAction? TryGetAndUnset()
        {
            return Interlocked.Exchange(ref _field, null);
        }

        public bool TryUpdateContext(Func<T, T> contextUpdater)
        {
            _ = contextUpdater ?? throw new ArgumentNullException(nameof(contextUpdater));
            while (true)
            {
                var original = Interlocked.CompareExchange(ref _field, _field, _field);
                if (original == null)
                    return false;
                var updatedContext = new BoundAction(original, contextUpdater);
                var result = Interlocked.CompareExchange(ref _field, updatedContext, original);
                if (ReferenceEquals(original, result))
                    return true;
            }
        }

        public interface IBoundAction
        {
            void Invoke();
        }

        private sealed class BoundAction : IBoundAction
        {
            private readonly Action<T> _action;
            private readonly T _context;

            public BoundAction(Action<T> action, T context)
            {
                _action = action;
                _context = context;
            }

            public BoundAction(BoundAction originalBoundAction, Func<T, T> contextUpdater)
            {
                _action = originalBoundAction._action;
                _context = contextUpdater(originalBoundAction._context);
            }

            public void Invoke() => _action?.Invoke(_context);
        }
    }
