#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

/// <summary>
/// This collection definition forces all RabbitMQ mTLS tests to run sequentially
/// instead of in parallel. This prevents TLS handshake race conditions that occur
/// when multiple tests rapidly create and destroy mTLS connections simultaneously.
///
/// Without this, tests can fail with "Connection close forced" errors due to
/// "TLS server: Unexpected Message" alerts from RabbitMQ when connections are
/// opened too rapidly in parallel.
/// </summary>
[CollectionDefinition("RabbitMQ mTLS", DisableParallelization = true)]
public class RabbitMQMtlsTestCollection
{
    // This class is never instantiated. It's just a marker for xUnit to identify
    // the collection and its parallelization settings.
}
