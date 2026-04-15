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

namespace Paramore.Brighter;

/// <summary>
/// Provides read-only introspection over the subscriber registry for startup validation.
/// </summary>
public interface IAmASubscriberRegistryInspector
{
    /// <summary>
    /// Gets all handler types registered for a given request type.
    /// </summary>
    /// <param name="requestType">The request type to look up.</param>
    /// <returns>A read-only collection of handler types registered for the request type.</returns>
    IReadOnlyCollection<Type> GetHandlerTypes(Type requestType);

    /// <summary>
    /// Gets all request types that have handlers registered.
    /// </summary>
    /// <returns>A read-only collection of all registered request types.</returns>
    IReadOnlyCollection<Type> GetRegisteredRequestTypes();
}
