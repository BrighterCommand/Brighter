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
}
