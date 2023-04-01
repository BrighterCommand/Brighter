using System.IO;
using FluentMigrator.Runner;
using GreetingsWeb.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GreetingsWeb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            host.CheckDbIsUp();
            host.MigrateDatabase();
            host.CreateOutbox();

            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, configBuilder) =>
                {
                    var env = context.HostingEnvironment;
                    configBuilder.AddJsonFile("appsettings.json", optional: false);
                    configBuilder.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);
                    configBuilder.AddEnvironmentVariables(prefix:"BRIGHTER_");
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel();
                    webBuilder.UseContentRoot(Directory.GetCurrentDirectory());
                    webBuilder.CaptureStartupErrors(true);
                    webBuilder.UseSetting("detailedErrors", "true");
                    webBuilder.ConfigureLogging((hostingContext, logging) =>
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
    }
}
