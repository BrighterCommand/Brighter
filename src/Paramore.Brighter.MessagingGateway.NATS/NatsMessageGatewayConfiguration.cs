// The MIT License (MIT)
// Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using NATS.Client.Core;
using NATS.Client.JetStream;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MessagingGateway.NATS;

/// <summary>
/// Configuration for the NATS messaging gateway: connection options for core NATS and JetStream.
/// </summary>
public class NatsMessageGatewayConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsMessageGatewayConfiguration"/> class with default options,
    /// connecting to <c>nats://localhost:4222</c>.
    /// </summary>
    public NatsMessageGatewayConfiguration()
    {
        
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsMessageGatewayConfiguration"/> class.
    /// </summary>
    /// <param name="natsOpts">The <see cref="NATS.Client.Core.NatsOpts"/> used to connect to the NATS server.</param>
    public NatsMessageGatewayConfiguration(NatsOpts natsOpts)
    {
        NatsOpts = natsOpts;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsMessageGatewayConfiguration"/> class.
    /// </summary>
    /// <param name="natsOpts">The <see cref="NATS.Client.Core.NatsOpts"/> used to connect to the NATS server.</param>
    /// <param name="natsJSOpts">The <see cref="NATS.Client.JetStream.NatsJSOpts"/> used for JetStream operations.</param>
    public NatsMessageGatewayConfiguration(NatsOpts natsOpts, NatsJSOpts natsJSOpts)
    {
        NatsOpts = natsOpts;
        NatsJsOpts = natsJSOpts;
    }

    /// <summary>
    /// Gets or sets the options used to connect to the NATS server.
    /// </summary>
    /// <value>The <see cref="NATS.Client.Core.NatsOpts"/>; defaults to a plain connection to <c>nats://localhost:4222</c>.</value>
    public NatsOpts NatsOpts { get; set; } = new NatsOpts();

    /// <summary>
    /// Gets or sets the options used for JetStream operations.
    /// </summary>
    /// <value>The <see cref="NATS.Client.JetStream.NatsJSOpts"/>, or <see langword="null"/> to derive defaults from <see cref="NatsOpts"/>.</value>
    public NatsJSOpts? NatsJsOpts { get; set; }

    /// <summary>
    /// Gets or sets how much telemetry the gateway writes.
    /// </summary>
    /// <value>The <see cref="Observability.InstrumentationOptions"/>; defaults to <see cref="InstrumentationOptions.All"/>.</value>
    public InstrumentationOptions Instrumentation { get; set; } = InstrumentationOptions.All;
}
