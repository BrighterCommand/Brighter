#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel <guilhermeaappel@gmail.com>

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

#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Gcp.Tests.TestDoubles;

internal sealed class HeldReplyConnections : IDisposable
{
    private readonly SocketsHttpHandler _handler;
    private readonly ConcurrentBag<Stream> _streams = new();
    private volatile bool _holdReplies;

    public HeldReplyConnections()
    {
        _handler = new SocketsHttpHandler { ConnectCallback = ConnectAsync };
    }

    public HttpMessageHandler Handler => _handler;

    public void HoldReplies()
    {
        _holdReplies = true;
    }

    public void Dispose()
    {
        _handler.Dispose();
        foreach (var stream in _streams)
            stream.Dispose();
    }

    private async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        var stream = new HeldReplyStream(new NetworkStream(socket, ownsSocket: true), () => _holdReplies);
        _streams.Add(stream);
        return stream;
    }
}
