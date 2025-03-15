using System.Diagnostics;
using DbMaker;
using TransportMaker;

namespace MigrationService;

public class Worker(
    IServiceProvider serviceProvider,
    IHostEnvironment hostEnvironment,
    IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    private string ActivitySourceName = hostEnvironment.ApplicationName;

    private readonly ActivitySource _activitySource = new(hostEnvironment.ApplicationName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity(hostEnvironment.ApplicationName, ActivityKind.Client);

        try
        {
            //host.CheckDbIsUp(ApplicationType.Greetings);
            using var scope = serviceProvider.CreateScope();

            var configuration = scope.ServiceProvider.GetService<IConfiguration>() ?? throw new InvalidOperationException();
            
            SchemaCreation.CheckDbIsUp(ApplicationType.Greetings, configuration);
            SchemaCreation.MigrateDatabase(scope.ServiceProvider);
            SchemaCreation.CreateOutbox(configuration, ConfigureTransport.HasBinaryMessagePayload(), ApplicationType.Greetings);
            
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }
}
