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
using System.Reflection;

namespace Paramore.Brighter;

/// <summary>
/// Judges whether a candidate <see cref="Subscription"/>'s declared <see cref="Subscription.ChannelFactoryType"/>
/// is a sound declaration, independently of any particular gateway assembly or channel factory instance.
/// </summary>
public static class SubscriptionChannelFactoryDeclaration
{
    /// <summary>
    /// Checks whether <paramref name="declaredFactoryType"/> is a sound declaration for
    /// <paramref name="subscriptionType"/>.
    /// </summary>
    /// <param name="subscriptionType">The candidate <see cref="Subscription"/> type being examined.</param>
    /// <param name="declaredFactoryType">The value <see cref="Subscription.ChannelFactoryType"/> returned
    /// for the candidate, or <c>null</c> when the declaration is absent.</param>
    /// <returns><c>null</c> when the declaration is sound; otherwise the single reason it is not.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="subscriptionType"/> is <c>null</c>.</exception>
    public static string? Check(Type subscriptionType, Type? declaredFactoryType)
    {
        if (subscriptionType is null)
            throw new ArgumentNullException(nameof(subscriptionType));

        // Null: ChannelFactoryType returned no type at all
        if (declaredFactoryType is null)
        {
            return $"Subscription type '{subscriptionType.FullName}' declares no channel factory type " +
                   "('ChannelFactoryType' returned null).";
        }

        // Not-a-channel-factory: the declared type exists but does not implement IAmAChannelFactory
        if (!typeof(IAmAChannelFactory).IsAssignableFrom(declaredFactoryType))
        {
            return $"Subscription type '{subscriptionType.FullName}' declares '{declaredFactoryType?.FullName}', " +
                   $"which does not implement '{typeof(IAmAChannelFactory).FullName}'.";
        }

        // Inherited-default: the declaration falls back to Subscription's own InMemoryChannelFactory default
        if (declaredFactoryType == typeof(InMemoryChannelFactory))
        {
            return $"Subscription type '{subscriptionType.FullName}' declares '{typeof(InMemoryChannelFactory).FullName}'. " +
                   "A shipped gateway subscription must declare its own transport's channel factory; a type that " +
                   "does not override 'ChannelFactoryType' inherits this default.";
        }

        return null;
    }

    /// <summary>
    /// Sweeps <paramref name="gatewayAssembly"/> for <see cref="Subscription"/> candidates and reports
    /// each subject with the reason its declared <see cref="Subscription.ChannelFactoryType"/> is
    /// unsound, or <c>null</c> when it is sound. A candidate is a non-abstract class that is
    /// <see cref="Subscription"/> itself or whose base chain reaches it. One entry per subject,
    /// ordered by <see cref="Type.FullName"/>, ordinal, so repeated runs are byte-identical.
    /// </summary>
    /// <param name="gatewayAssembly">The assembly to sweep for <see cref="Subscription"/> candidates.</param>
    /// <returns>One entry per subject; <c>Reason</c> is <c>null</c> when the declaration is sound.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="gatewayAssembly"/> is <c>null</c>.</exception>
    public static IReadOnlyList<(Type Subject, string? Reason)> Sweep(Assembly gatewayAssembly)
    {
        if (gatewayAssembly is null)
            throw new ArgumentNullException(nameof(gatewayAssembly));

        return gatewayAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && IsSubscriptionOrDerivedFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(ReadAndCheck)
            .ToList();
    }

    // Walked explicitly rather than tested with IsAssignableFrom, which behaves surprisingly for open
    // generic type definitions. A type is assignable to itself, so Subscription itself is a candidate.
    private static bool IsSubscriptionOrDerivedFrom(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current == typeof(Subscription))
                return true;
        }

        return false;
    }

    // GetUninitializedObject produces an instance with no constructor run, so Check never sees a
    // side effect of construction and can examine a subscription whose constructor would throw.
    // The reported Subject is always the candidate as discovered - an open generic definition is
    // closed only to obtain an instance to read; the closed construction is never itself reported.
    private static (Type Subject, string? Reason) ReadAndCheck(Type subject)
    {
        var readableType = CloseIfGeneric(subject);
        var instance = (Subscription)GetUninitializedInstance(readableType);
        return (subject, Check(subject, instance.ChannelFactoryType));
    }

    // Closes an open generic Subscription subclass against one representative IRequest argument,
    // Command, which satisfies both constraint forms the gateways use ('where T : IRequest' and
    // 'where T : class, IRequest'). GetUninitializedObject cannot instantiate an open definition.
    private static Type CloseIfGeneric(Type type) =>
        type.IsGenericTypeDefinition ? type.MakeGenericType(typeof(Command)) : type;

#if NETSTANDARD2_0
    private static object GetUninitializedInstance(Type type) =>
        System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
#else
    private static object GetUninitializedInstance(Type type) =>
        System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
#endif
}
