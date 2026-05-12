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

internal static class MapperMethodDiscovery
{
    /// <summary>
    /// Finds the MapToMessage method on a sync mapper type for a given request type.
    /// Signature: MapToMessage(TRequest, Publication)
    /// </summary>
    public static MethodInfo? FindMapToMessage(Type mapperType, Type requestType)
    {
        return mapperType.GetMethods()
            .Where(method => method.Name == nameof(IAmAMessageMapper<IRequest>.MapToMessage))
            .SingleOrDefault(
                method => method.GetParameters().Length == 2
                    && method.GetParameters().First().ParameterType == requestType
                    && method.GetParameters().Last().ParameterType == typeof(Publication)
            );
    }

    /// <summary>
    /// Finds the MapToRequest method on a sync mapper type.
    /// Signature: MapToRequest(Message)
    /// </summary>
    public static MethodInfo? FindMapToRequest(Type mapperType)
    {
        return mapperType.GetMethods()
            .Where(method => method.Name == nameof(IAmAMessageMapper<IRequest>.MapToRequest))
            .SingleOrDefault(
                method => method.GetParameters().Length == 1
                    && method.GetParameters().Single().ParameterType == typeof(Message)
            );
    }

    /// <summary>
    /// Finds the MapToMessageAsync method on an async mapper type for a given request type.
    /// Signature: MapToMessageAsync(TRequest, Publication, CancellationToken)
    /// </summary>
    public static MethodInfo? FindMapToMessageAsync(Type mapperType, Type requestType)
    {
        return mapperType.GetMethod(
            nameof(IAmAMessageMapperAsync<IRequest>.MapToMessageAsync),
            BindingFlags.Public | BindingFlags.Instance,
            null,
            CallingConventions.Any,
            [requestType, typeof(Publication), typeof(CancellationToken)],
            null);
    }

    /// <summary>
    /// Finds the MapToRequestAsync method on an async mapper type.
    /// Signature: MapToRequestAsync(Message, CancellationToken)
    /// </summary>
    public static MethodInfo? FindMapToRequestAsync(Type mapperType)
    {
        return mapperType.GetMethod(
            nameof(IAmAMessageMapperAsync<IRequest>.MapToRequestAsync),
            BindingFlags.Public | BindingFlags.Instance,
            null,
            CallingConventions.Any,
            [typeof(Message), typeof(CancellationToken)],
            null);
    }
}
