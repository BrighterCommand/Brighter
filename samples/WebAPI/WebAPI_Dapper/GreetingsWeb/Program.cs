using System.IO;
using FluentMigrator.Runner;
using DbMaker;
using GreetingsWeb;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Extensions.Diagnostics;
using TransportMaker;

IHost host = CreateHostBuilder(args).Build();

host.CheckDbIsUp(ApplicationType.Greetings);
host.MigrateDatabase();
host.CreateOutbox(ApplicationType.Greetings, "Greetings", ConfigureTransport.HasBinaryMessagePayload());

host.Run();
return;

static IHostBuilder CreateHostBuilder(string[] args)
{
    return Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, configBuilder) =>
        {
            IHostEnvironment env = context.HostingEnvironment;
            configBuilder.AddJsonFile("appsettings.json", false);
            configBuilder.AddJsonFile($"appsettings.{env.EnvironmentName}.json", true);
            configBuilder.AddEnvironmentVariables("BRIGHTER_");
        })
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseKestrel();
            webBuilder.UseContentRoot(Directory.GetCurrentDirectory());
            webBuilder.CaptureStartupErrors(true);
            webBuilder.UseSetting("detailedErrors", "true");
            webBuilder.ConfigureLogging((_, logging) =>
            {
                logging.AddConsole();
                logging.AddDebug();
                logging.AddFluentMigratorConsole();
            });
            webBuilder.UseDefaultServiceProvider((context, options) =>
            {
                bool isDevelopment = context.HostingEnvironment.IsDevelopment();
                options.ValidateScopes = isDevelopment;
                options.ValidateOnBuild = isDevelopment;
            });

            webBuilder.UseStartup<Startup>();
        });


}
