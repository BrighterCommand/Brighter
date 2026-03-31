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

namespace Paramore.Brighter.ServiceActivator.Validation;

/// <summary>
/// Provides validation specifications for consumer subscriptions. Each method returns
/// an <see cref="ISpecification{T}"/> that evaluates a <see cref="Subscription"/>
/// and reports validation findings via the visitor pattern.
/// </summary>
public static class ConsumerValidationRules
{
    /// <summary>
    /// Validates that the subscription's <see cref="Subscription.MessagePumpType"/> matches the
    /// sync/async nature of all registered handlers. A Reactor subscription with an async handler
    /// (or a Proactor subscription with a sync handler) means the handler will not be invoked correctly.
    /// Vacuously passes when no handlers are registered (caught by <see cref="HandlerRegistered"/>).
    /// </summary>
    /// <param name="inspector">The subscriber registry inspector to look up handler types.</param>
    /// <returns>A simple specification that reports an Error when pump type and handler type are mismatched.</returns>
    public static ISpecification<Subscription> PumpHandlerMatch(IAmASubscriberRegistryInspector inspector)
        => new Specification<Subscription>(
            s =>
            {
                if (s.RequestType == null) return true;

                var handlerTypes = inspector.GetHandlerTypes(s.RequestType);
                if (handlerTypes.Count == 0) return true;

                return s.MessagePumpType switch
                {
                    MessagePumpType.Reactor => handlerTypes.All(h => !IsAsyncHandler(h)),
                    MessagePumpType.Proactor => handlerTypes.All(h => IsAsyncHandler(h)),
                    _ => true
                };
            },
            s =>
            {
                var handlerTypes = inspector.GetHandlerTypes(s.RequestType!);
                var mismatchedHandler = s.MessagePumpType switch
                {
                    MessagePumpType.Reactor => handlerTypes.First(h => IsAsyncHandler(h)),
                    MessagePumpType.Proactor => handlerTypes.First(h => !IsAsyncHandler(h)),
                    _ => handlerTypes.First()
                };

                return s.MessagePumpType switch
                {
                    MessagePumpType.Reactor => new ValidationError(
                        ValidationSeverity.Error,
                        $"Subscription '{s.Name}'",
                        $"Subscription uses Reactor (sync) pump but handler '{mismatchedHandler.Name}' is async " +
                        "— use Proactor for async handlers"),
                    MessagePumpType.Proactor => new ValidationError(
                        ValidationSeverity.Error,
                        $"Subscription '{s.Name}'",
                        $"Subscription uses Proactor (async) pump but handler '{mismatchedHandler.Name}' is sync " +
                        "— use Reactor for sync handlers"),
                    _ => new ValidationError(
                        ValidationSeverity.Error,
                        $"Subscription '{s.Name}'",
                        "Unknown pump type mismatch")
                };
            });

    /// <summary>
    /// Validates that there is at least one handler registered for the subscription's
    /// <see cref="Subscription.RequestType"/>. A subscription with no handler means messages
    /// will be received but cannot be dispatched.
    /// Vacuously passes when RequestType is null (caught by other validation rules).
    /// </summary>
    /// <param name="inspector">The subscriber registry inspector to look up handler types.</param>
    /// <returns>A simple specification that reports an Error when no handler is registered.</returns>
    public static ISpecification<Subscription> HandlerRegistered(IAmASubscriberRegistryInspector inspector)
        => new Specification<Subscription>(
            s => s.RequestType == null || inspector.GetHandlerTypes(s.RequestType).Count > 0,
            s => new ValidationError(
                ValidationSeverity.Error,
                $"Subscription '{s.Name}'",
                $"No handler registered for '{s.RequestType!.Name}' — messages will be received but cannot be dispatched"));

    /// <summary>
    /// Validates that the subscription's <see cref="Subscription.RequestType"/> implements either
    /// <see cref="ICommand"/> or <see cref="IEvent"/>. A type that only implements <see cref="IRequest"/>
    /// directly will work but is unusual and may indicate a misconfiguration.
    /// Vacuously passes when RequestType is null.
    /// </summary>
    /// <returns>A simple specification that reports a Warning when RequestType implements neither ICommand nor IEvent.</returns>
    public static ISpecification<Subscription> RequestTypeSubtype()
        => new Specification<Subscription>(
            s => s.RequestType == null
                 || typeof(ICommand).IsAssignableFrom(s.RequestType)
                 || typeof(IEvent).IsAssignableFrom(s.RequestType),
            s => new ValidationError(
                ValidationSeverity.Warning,
                $"Subscription '{s.Name}'",
                $"RequestType '{s.RequestType!.Name}' implements neither ICommand nor IEvent " +
                "— consider implementing one of these marker interfaces"));

    /// <summary>
    /// Checks whether <paramref name="handlerType"/> derives from <c>RequestHandlerAsync&lt;&gt;</c>.
    /// Walks the base type chain so it works with both open and closed generic types.
    /// </summary>
    private static bool IsAsyncHandler(Type handlerType)
    {
        var type = handlerType;
        while (type != null)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(RequestHandlerAsync<>))
                return true;
            type = type.BaseType;
        }

        return false;
    }
}
