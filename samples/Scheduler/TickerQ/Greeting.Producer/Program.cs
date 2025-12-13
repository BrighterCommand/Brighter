using Greeting.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessageScheduler.TickerQ;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using TickerQ.Dashboard.DependencyInjection;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.DbContextFactory;
using TickerQ.EntityFrameworkCore.DependencyInjection;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces;
using TickerQ.Utilities.Interfaces.Managers;


var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("messaging");
builder.Services.AddTickerQ(options =>
{
    options.AddOperationalStore(efOptions =>
    {
        efOptions.UseTickerQDbContext<TickerQDbContext>(dbOptions =>
        {
            dbOptions.UseSqlite(
                "Data Source=tickerq-brighter-sample.db",
               b => b.MigrationsAssembly(typeof(Program).Assembly));
        });
    });
    options.AddDashboard(o =>
    {
        o.SetBasePath("/dashboard");
    });
    options.ConfigureScheduler(c =>
    {
        c.SchedulerTimeZone = TimeZoneInfo.Utc;
    });
});
builder.AddServiceDefaults();
var cnstring = builder.Configuration.GetConnectionString("messaging") ?? "amqp://guest:guest@localhost:5672/";
var rmqConnection = new RmqMessagingGatewayConnection
{
    AmpqUri = new AmqpUriSpecification(new Uri(cnstring)),
    Exchange = new Exchange("paramore.brighter.exchange"),
};

builder.Services.AddBrighter().AddProducers(c =>
{
    c.ProducerRegistry = new RmqProducerRegistryFactory(
                            rmqConnection,
                             [
                                new()
                                {
                                    Topic = new RoutingKey("greeting.event"),
                                    RequestType = typeof(GreetingEvent),
                                    MakeChannels = OnMissingChannel.Create
                                }
                             ]).Create();

}).UseScheduler(provider =>
{
    var timeTickerManager = provider.GetRequiredService<ITimeTickerManager<TimeTickerEntity>>();
    var persistenceProvider = provider.GetRequiredService<ITickerPersistenceProvider<TimeTickerEntity, CronTickerEntity>>();
    var timeprovider = provider.GetRequiredService<TimeProvider>();
    return new TickerQSchedulerFactory(timeTickerManager, persistenceProvider, timeprovider);
});

builder.Services.AddHostedService<ProducerHostedService>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TickerQDbContext>();
    db.Database.Migrate();
}
app.UseTickerQ();
app.MapDefaultEndpoints();

app.UseHttpsRedirection();

app.MapGet("/", () =>
{
    return "helloProducer";
});

app.Run();
public class ProducerHostedService(IAmACommandProcessor commandProcessor, ILogger<ProducerHostedService> logger) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (int i = 0; i < 100; i++)
        {
            logger.LogInformation("Scheduling message #{Loop}", i);
            await commandProcessor.PostAsync(TimeSpan.FromSeconds(2*(i+3)), new GreetingEvent($"Scheduler message  #{i}"));
            logger.LogInformation("Pausing for breath..."); 
            await Task.Delay(4000, stoppingToken);
        }
    }
}
