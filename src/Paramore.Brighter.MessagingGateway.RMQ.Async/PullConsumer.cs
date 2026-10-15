#region Licence

/* The MIT License (MIT)
Copyright © 2019 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paramore.Brighter.MessagingGateway.RMQ.Async;

public partial class PullConsumer(IChannel channel) : AsyncDefaultBasicConsumer(channel)
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessageConsumer>();

    //we do end up creating a second buffer to the Brighter Channel, but controlling the flow from RMQ depends
    //on us being able to buffer up to the set QoS and then pull. This matches other implementations.
    private readonly ConcurrentQueue<BasicDeliverEventArgs> _messages = new();
    private volatile TaskCompletionSource<bool> _cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _purging;

    // The cancel callback follows pending deliveries; the broker's cancel reply alone is not a buffer barrier.
    internal async Task stopForPurgeAsync(string consumerTag)
    {
        Volatile.Write(ref _purging, true);
        Task<bool> cancelled = _cancelled.Task;

        try
        {
            await Channel.BasicCancelAsync(consumerTag);
            await waitForCancellationAsync(cancelled);

            while (_messages.TryDequeue(out var message))
            {
                await Channel.BasicAckAsync(message.DeliveryTag, false);
            }
        }
        catch
        {
            await Channel.AbortAsync();
            throw;
        }
    }

    private async Task waitForCancellationAsync(Task cancelled)
    {
        using var timeout = new CancellationTokenSource();
        Task delay = Task.Delay(Channel.ContinuationTimeout, timeout.Token);
        if (await Task.WhenAny(cancelled, delay) != cancelled)
        {
            throw new TimeoutException("Timed out waiting for RabbitMQ consumer cancellation.");
        }

        timeout.Cancel();
        await cancelled;
    }

    /// <inheritdoc />
    public override async Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason)
    {
        await base.HandleChannelShutdownAsync(channel, reason);
        while (_messages.TryDequeue(out _)) { }
        _cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Sets the number of messages to fetch from the broker in a single batch
    /// </summary>
    /// <remarks>
    /// Works on BasicConsume, no impact on BasicGet§
    /// </remarks>
    /// <param name="batchSize">The batch size defaults to 1 unless set on subscription</param>
    public async Task SetChannelBatchSizeAsync(ushort batchSize = 1)
    {
        await Channel.BasicQosAsync(0, batchSize, false);
    }

    /// <summary>
    /// Used to pull from the buffer of messages delivered to us via BasicConsumer
    /// </summary>
    /// <param name="timeOut">The total time to spend waiting for the buffer to fill up to bufferSize</param>
    /// <param name="bufferSize">The size of the buffer we want to fill wit messages</param>
    /// <returns>A tuple containing: the number of messages in the buffer, and the buffer itself</returns>
    public async Task<(int bufferIndex, BasicDeliverEventArgs[]? buffer)> DeQueue(TimeSpan timeOut, int bufferSize)
    {
        var now = DateTime.UtcNow;
        var end = now.Add(timeOut);
        var pause = (timeOut > TimeSpan.FromMilliseconds(25)) ? Convert.ToInt32(timeOut.TotalMilliseconds) / 5 : 5;


        var buffer = new BasicDeliverEventArgs[bufferSize];
        var bufferIndex = 0;


        while (now < end && bufferIndex < bufferSize)
        {
            if (_messages.TryDequeue(out BasicDeliverEventArgs? result))
            {
                buffer[bufferIndex] = result;
                ++bufferIndex;
            }
            else
            {
                await Task.Delay(pause);
            }

            now = DateTime.UtcNow;
        }

        return bufferIndex == 0 ? (0, Array.Empty<BasicDeliverEventArgs>()) : (bufferIndex, buffer);
    }

    public override Task HandleBasicDeliverAsync(string consumerTag,
        ulong deliveryTag,
        bool redelivered,
        string exchange,
        string routingKey,
        IReadOnlyBasicProperties properties,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        //We have to copy the body, before returning, as the memory in body is pooled and may be re-used after (see base class documentation)
        //See also https://docs.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines
        var payload = new byte[body.Length];
        body.CopyTo(payload);


        _messages.Enqueue(new BasicDeliverEventArgs(consumerTag,
            deliveryTag,
            redelivered,
            exchange,
            routingKey,
            properties,
            payload,
            cancellationToken));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override async Task OnCancelAsync(string[] consumerTags,
        CancellationToken cancellationToken = default)
    {
        try
        {
            while (!Volatile.Read(ref _purging) && _messages.TryDequeue(out var message))
            {
                await Channel.BasicNackAsync(message.DeliveryTag, false, true, cancellationToken);
            }
        }
        catch (Exception e)
        {
            Log.NackUnhandledMessagesOnShutdownFailed(s_logger, e.Message);
        }

        await base.OnCancelAsync(consumerTags, cancellationToken);
        _cancelled.TrySetResult(true);
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "Tried to nack unhandled messages on shutdown but failed for {ErrorMessage}")]
        public static partial void NackUnhandledMessagesOnShutdownFailed(ILogger logger, string errorMessage);
    }
}

