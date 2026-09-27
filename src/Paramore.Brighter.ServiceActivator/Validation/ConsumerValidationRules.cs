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
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Validation;

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
    /// Validates that a subscription's declared <see cref="Subscription.ChannelFactoryType"/> is
    /// compatible with the channel factory it will actually be handed at startup — either its own
    /// <see cref="Subscription.ChannelFactory"/>, or, absent that, <paramref name="defaultChannelFactory"/>.
    /// Deliberately does not vacuously pass when <see cref="Subscription.RequestType"/> is null.
    /// </summary>
    /// <param name="defaultChannelFactory">The consumer options' default channel factory, or null.
    /// Used only when the subscription carries no factory of its own.</param>
    /// <returns>A simple specification that reports an Error when the declared and effective channel
    /// factory types are incompatible.</returns>
    public static ISpecification<Subscription> ChannelFactoryCompatible(IAmAChannelFactory? defaultChannelFactory)
        => new Specification<Subscription>(
            s =>
            {
                var (arm, candidates) = ResolveCandidates(s, defaultChannelFactory);
                return IsCompatible(s.ChannelFactoryType, arm, candidates);
            },
            s =>
            {
                var (arm, candidates) = ResolveCandidates(s, defaultChannelFactory);
                var declared = s.ChannelFactoryType;
                var declaredClause = declared is null
                    ? "declares no ChannelFactoryType"
                    : $"declares ChannelFactoryType '{DisplayName(declared)}'";
                var handedClause = candidates.Count == 0
                    ? "no channel factory at all"
                    : arm == Arm.Combined
                        ? $"one of '{string.Join(", ", candidates.Select(DisplayName))}'"
                        : $"'{string.Join(", ", candidates.Select(DisplayName))}'";
                var remedy = RemedyClause(declared, arm, candidates);
                return new ValidationError(
                    ValidationSeverity.Error,
                    $"Subscription '{s.Name}'",
                    $"Subscription type '{DisplayName(s.GetType())}' {declaredClause} but will be handed {handedClause} {remedy}");
            });

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
    /// Validates that every unwrap transform the subscription's resolved mapper declares can be resolved.
    /// A declared transform whose transformer type is not registered is a strong signal that its assembly
    /// was not scanned. Reports one <see cref="ValidationSeverity.Warning"/> per
    /// unresolvable transform, naming the request type, the transformer type, the subscription, and prompting an
    /// <c>AutoFromAssemblies</c> check. Subscriptions with a null <see cref="Subscription.RequestType"/>, whose
    /// request type resolves to no mapper, or whose request type resolves to the default mapper (whose transforms
    /// are Brighter built-ins and out of scope) are skipped.
    /// </summary>
    /// <param name="mapperRegistryFactory">Builds the mapper registry used to describe the subscription's
    /// transforms. The rule invokes this once and takes <b>ownership</b> of the registry it returns, disposing
    /// it (draining its factories) when the specification is disposed. Taking a factory rather than a live
    /// instance keeps that ownership transfer explicit — the rule disposes only a registry it created — so a
    /// caller cannot hand in a registry it still uses elsewhere and have it disposed underneath them.</param>
    /// <param name="probe">Answers whether a declared transformer type is resolvable, without instantiating it.</param>
    /// <returns>A specification that reports a Warning per unresolvable unwrap transform, and that disposes the
    /// registry <paramref name="mapperRegistryFactory"/> produced when the container disposes it.</returns>
    public static ISpecification<Subscription> UnwrapTransformResolvable(
        Func<MessageMapperRegistry> mapperRegistryFactory, IAmATransformerResolvabilityProbe probe)
    {
        var mapperRegistry = mapperRegistryFactory();
        return new DisposingSpecification<Subscription>(subscription =>
        {
            if (subscription.RequestType is null)
                return [];

            var description = TransformPipelineBuilder.DescribeTransforms(
                mapperRegistry, subscription.RequestType, includeAsync: true);

            if (description is null || description.IsDefaultMapper)
                return [];

            return description.UnwrapTransforms
                .Where(step => !probe.Resolves(step.TransformType))
                .Select(step => ValidationResult.Fail(new ValidationError(
                    ValidationSeverity.Warning,
                    $"Subscription '{subscription.Name}'",
                    $"Request '{subscription.RequestType.Name}' declares unwrap transform '{step.TransformType.Name}' " +
                    $"on subscription '{subscription.Name}' — that transformer is not registered. " +
                    "Is its assembly included in AutoFromAssemblies()?")))
                .ToList();
        }, mapperRegistry);
    }

    /// <summary>
    /// Which routing arm <see cref="ChannelFactoryCompatible"/> is evaluating: a single factory
    /// directly, or the inner factories of a <see cref="CombinedChannelFactory"/>.
    /// </summary>
    private enum Arm
    {
        Direct,
        Combined
    }

    /// <summary>
    /// Resolves the arm and the ordered candidate <see cref="Type"/> list a subscription's declared
    /// <see cref="Subscription.ChannelFactoryType"/> is compared against.
    /// </summary>
    private static (Arm arm, IReadOnlyList<Type> candidates) ResolveCandidates(
        Subscription subscription, IAmAChannelFactory? defaultChannelFactory)
    {
        var effective = subscription.ChannelFactory ?? defaultChannelFactory;
        if (effective is CombinedChannelFactory combined)
            return (Arm.Combined, combined.FactoryTypes);

        return (Arm.Direct, effective is null ? [typeof(InMemoryChannelFactory)] : [effective.GetType()]);
    }

    /// <summary>
    /// Decides whether a subscription's declared type is compatible with the resolved candidates.
    /// </summary>
    private static bool IsCompatible(Type? declared, Arm arm, IReadOnlyList<Type> candidates)
        => arm switch
        {
            Arm.Direct => declared is not null && declared.IsAssignableFrom(candidates[0]),
            Arm.Combined => declared is not null && candidates.Any(t => t == declared),
            _ => false
        };

    /// <summary>
    /// Renders the finding message's remedy clause, per FR-5's ordered template table.
    /// </summary>
    private static string RemedyClause(Type? declared, Arm arm, IReadOnlyList<Type> candidates)
    {
        if (arm == Arm.Combined && candidates.Count == 0)
            return "— add a channel factory to the combined channel factory";

        var suppressed = declared is null || declared == typeof(InMemoryChannelFactory);
        var handed = string.Join(", ", candidates.Select(DisplayName));

        if (arm == Arm.Direct && suppressed)
            return $"— use a subscription type whose ChannelFactoryType is {DisplayName(candidates[0])}";

        if (arm == Arm.Combined && suppressed)
            return $"— use a subscription type whose ChannelFactoryType is one of: {handed}";

        var subscriptionSide = arm == Arm.Combined
            ? $"one of: {handed}"
            : handed;
        return $"— either configure a channel factory of type {DisplayName(declared!)}, " +
               $"or use a subscription type whose ChannelFactoryType is {subscriptionSide}";
    }

    /// <summary>
    /// Renders a type for a validation message: its full name, namespace-qualified rather than
    /// assembly-qualified. A generic type renders as <c>Namespace.Type&lt;Arg1, Arg2&gt;</c>, with
    /// each type argument itself rendered through <see cref="DisplayName"/>.
    /// </summary>
    private static string DisplayName(Type type)
    {
        if (!type.IsGenericType)
            return type.FullName ?? type.Name;

        var definitionName = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var backtickIndex = definitionName.IndexOf('`');
        if (backtickIndex >= 0)
            definitionName = definitionName.Substring(0, backtickIndex);

        var args = string.Join(", ", type.GetGenericArguments().Select(DisplayName));
        return $"{definitionName}<{args}>";
    }

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
