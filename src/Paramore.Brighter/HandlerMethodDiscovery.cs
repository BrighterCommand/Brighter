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
using System.Reflection;
using System.Threading;

namespace Paramore.Brighter;

internal static class HandlerMethodDiscovery
{
    /// <summary>
    /// Finds the Handle or HandleAsync method on a handler type for a given request type.
    /// Determines sync vs async from the handler's type hierarchy.
    /// </summary>
    /// <param name="handlerType">The concrete handler type to inspect.</param>
    /// <param name="requestType">The request type the handler processes.</param>
    /// <returns>The <see cref="MethodInfo"/> for the handler method.</returns>
    public static MethodInfo FindHandlerMethod(Type handlerType, Type requestType)
    {
        var methods = handlerType.GetMethods();

        if (IsAsyncHandler(handlerType))
        {
            return methods
                .Where(method => method.Name == nameof(RequestHandlerAsync<IRequest>.HandleAsync))
                .Single(method => method.GetParameters().Length == 2
                    && method.GetParameters()[0].ParameterType == requestType
                    && method.GetParameters()[1].ParameterType == typeof(CancellationToken));
        }

        return methods
            .Where(method => method.Name == nameof(RequestHandler<IRequest>.Handle))
            .Single(method => method.GetParameters().Length == 1
                && method.GetParameters().Single().ParameterType == requestType);
    }

    internal static bool IsAsyncHandler(Type handlerType)
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
