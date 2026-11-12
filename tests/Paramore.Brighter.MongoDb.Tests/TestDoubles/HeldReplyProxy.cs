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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MongoDb.Tests.TestDoubles;

internal sealed class HeldReplyProxy : IDisposable
{
    private const int BufferSize = 81920;

    private readonly string _targetHost;
    private readonly int _targetPort;
    private readonly TcpListener _listener;
    private readonly ConcurrentBag<TcpClient> _sockets = new();
    private readonly CancellationTokenSource _cancellation = new();
    private volatile bool _holdReplies;
    private bool _disposed;

    public HeldReplyProxy(string targetHost, int targetPort)
    {
        _targetHost = targetHost;
        _targetPort = targetPort;
        _listener = new TcpListener(IPAddress.Loopback, 0);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start()
    {
        _listener.Start();
        _ = AcceptClientsAsync();
    }

    public void HoldReplies()
    {
        _holdReplies = true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _cancellation.Cancel();
        _listener.Stop();
        foreach (var socket in _sockets)
            socket.Dispose();
        _cancellation.Dispose();
    }

    private async Task AcceptClientsAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                _sockets.Add(client);
                _ = RelayAsync(client);
            }
            catch (Exception)
            {
                return;
            }
        }
    }

    private async Task RelayAsync(TcpClient client)
    {
        try
        {
            var upstream = new TcpClient();
            _sockets.Add(upstream);
            await upstream.ConnectAsync(_targetHost, _targetPort);
            var clientStream = client.GetStream();
            var upstreamStream = upstream.GetStream();
            await Task.WhenAll(
                PumpAsync(clientStream, upstreamStream, dropReplies: false),
                PumpAsync(upstreamStream, clientStream, dropReplies: true));
        }
        catch (Exception)
        {
        }
    }

    private async Task PumpAsync(NetworkStream source, NetworkStream destination, bool dropReplies)
    {
        var buffer = new byte[BufferSize];
        while (!_cancellation.IsCancellationRequested)
        {
            var read = await source.ReadAsync(buffer, 0, buffer.Length);
            if (read == 0)
                return;
            if (dropReplies && _holdReplies)
                continue;
            await destination.WriteAsync(buffer, 0, read);
        }
    }
}
