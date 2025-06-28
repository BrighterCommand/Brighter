using System.Diagnostics;
using GreetingsApp.EntityGateway;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace MigrationService;

public class GreetingsMigrator(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    public const string ActivitySourceName = "Greetings Migrations";
    private static readonly ActivitySource s_activitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Migrating Greeting database", ActivityKind.Client);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var greetingsDbContext = scope.ServiceProvider.GetRequiredService<GreetingsEntityGateway>();
            //var salutationsDbContext = scope.ServiceProvider.GetRequiredService<SalutationsEntityGateway>();

            await EnsureDatabaseAsync(greetingsDbContext, cancellationToken);
            await RunMigrationAsync(greetingsDbContext, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }

    private static async Task EnsureDatabaseAsync(GreetingsEntityGateway dbContext, CancellationToken cancellationToken)
    {
        var dbCreator = dbContext.GetService<IRelationalDatabaseCreator>();

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Create the database if it does not exist.
            // Do this first so there is then a database to start a transaction against.
            if (!await dbCreator.ExistsAsync(cancellationToken))
            {
                await dbCreator.CreateAsync(cancellationToken);
            }
        });
    }

    private static async Task RunMigrationAsync(GreetingsEntityGateway dbContext, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Run migration in a transaction to avoid partial migration if it fails.
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await dbContext.Database.MigrateAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}

// public class Worker(
//     IServiceProvider serviceProvider,
//     IHostEnvironment hostEnvironment,
//     IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
// {
//     private string ActivitySourceName = hostEnvironment.ApplicationName;
//
//     private readonly ActivitySource _activitySource = new(hostEnvironment.ApplicationName);
//
//     protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//     {
//         using var activity = _activitySource.StartActivity(hostEnvironment.ApplicationName, ActivityKind.Client);
//
//         try
//         {
//             //host.CheckDbIsUp(ApplicationType.Greetings);
//             using var scope = serviceProvider.CreateScope();
//
//             var configuration = scope.ServiceProvider.GetService<IConfiguration>() ?? throw new InvalidOperationException();
//             
//             SchemaCreation.CheckDbIsUp(ApplicationType.Greetings, configuration);
//             SchemaCreation.MigrateDatabase(scope.ServiceProvider);
//             SchemaCreation.CreateOutbox(configuration, ConfigureTransport.HasBinaryMessagePayload(), ApplicationType.Greetings);
//             
//         }
//         catch (Exception ex)
//         {
//             activity?.AddException(ex);
//             throw;
//         }
//
//         hostApplicationLifetime.StopApplication();
//     }
// }
