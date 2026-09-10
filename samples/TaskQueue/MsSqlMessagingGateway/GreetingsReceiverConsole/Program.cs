#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Events.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.MsSql;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// The same three tables the sender names, in the same database.
var configuration = new RelationalDatabaseConfiguration(
    @"Database=BrighterSqlQueue;Server=.\sqlexpress;Integrated Security=SSPI;",
    databaseName: "BrighterSqlQueue",
    outBoxTableName: "Outbox",
    inboxTableName: "InboxMessages",
    queueStoreTable: "QueueData");

builder.Services.AddConsumers(options =>
{
    options.Subscriptions =
    [
        // MsSqlSubscription, NOT Subscription: the MSSQL ChannelFactory casts what it is
        // given down to MsSqlSubscription and throws ConfigurationException when the cast
        // fails. A plain Subscription<T> compiles and then dies as the Dispatcher starts.
        new MsSqlSubscription<GreetingEvent>(
            new SubscriptionName("paramore.example.greeting"),
            new ChannelName("greeting.event"),
            new RoutingKey("greeting.event"),
            timeOut: TimeSpan.FromMilliseconds(200),
            messagePumpType: MessagePumpType.Reactor)
    ];
    options.DefaultChannelFactory = new ChannelFactory(
        new MsSqlMessageConsumerFactory(configuration)
    );

    // The Inbox makes the consumer idempotent: a redelivered message is recognised rather
    // than reprocessed. Warn rather than Throw, so a duplicate logs instead of raising.
    options.InboxConfiguration = new InboxConfiguration(
        new MsSqlInbox(configuration),
        scope: InboxScope.All,
        onceOnly: true,
        actionOnExists: OnceOnlyAction.Warn);
})
// InMemorySchedulerFactory is the default — shown here explicitly to demonstrate scheduler configuration.
// Replace with HangfireMessageSchedulerFactory or QuartzSchedulerFactory for durable scheduling.
.UseScheduler(new InMemorySchedulerFactory())
.AutoFromAssemblies();

builder.Services.AddHostedService<ServiceActivatorHostedService>();

var host = builder.Build();
await host.RunAsync();
