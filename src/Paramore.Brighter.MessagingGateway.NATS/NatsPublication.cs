#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

namespace Paramore.Brighter.MessagingGateway.NATS;

/// <summary>
/// Publication configuration for the NATS messaging gateway.
/// </summary>
public class NatsPublication : Publication
{
    
}

/// <summary>
/// Represents a publication for NATS, associating a specific message type with the publication.
/// </summary>
/// <typeparam name="T">The type of request that this publication handles.</typeparam>
public class NatsPublication<T> : NatsPublication
    where T : class, IRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsPublication{T}"/> class.
    /// </summary>
    public NatsPublication()
    {
        RequestType = typeof(T);
    }
}
