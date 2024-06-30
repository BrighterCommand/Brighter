using System;
using System.IO;
using FluentMigrator.Runner;
using GreetingsDb;
using GreetingsPorts.Messaging;
using GreetingsWeb;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = CreateHostBuilder(args).Build();

host.CheckDbIsUp();
host.MigrateDatabase();
host.CreateOutbox(HasBinaryMessagePayload());

host.Run();
return;

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, configBuilder) =>
        {
            var env = context.HostingEnvironment;
            configBuilder.AddJsonFile("appsettings.json", optional: false);
            configBuilder.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);
            configBuilder.AddEnvironmentVariables(prefix: "BRIGHTER_");
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
                var isDevelopment = context.HostingEnvironment.IsDevelopment();
                options.ValidateScopes = isDevelopment;
                options.ValidateOnBuild = isDevelopment;
            });
            webBuilder.UseStartup<Startup>();
        });

static bool HasBinaryMessagePayload()
{
    return ConfigureTransport.TransportType(Environment.GetEnvironmentVariable("BRIGHTER_TRANSPORT")) == MessagingTransport.Kafka;
}
