﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.ServiceActivator.Control.Sender;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Control.Hosting;

public class HeartbeatHostedService : IHostedService
{
    private readonly IDispatcher _dispatcher;
    private readonly ILogger _logger;
    private Timer? _timer;

    private int _heartbeatInterval = 5;

    public HeartbeatHostedService(IDispatcher dispatcher, ILogger<HeartbeatHostedService> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Heartbeat service");
        _timer = new Timer(SendHeartbeat, null, TimeSpan.Zero, TimeSpan.FromSeconds(_heartbeatInterval));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping heartbeat service");
        if (_timer != null)
            await _timer.DisposeAsync();
    }

    private void SendHeartbeat(object? state)
    {
        _logger.LogInformation("Sending Heartbeat");

        var factory = ((Dispatcher)_dispatcher).CommandProcessorFactory.Invoke();
        
        factory.CreateScope();

        HeartBeatSender.Send(factory.Get(), _dispatcher);
        
        factory.ReleaseScope();
    }
}
