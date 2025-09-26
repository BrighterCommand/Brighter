using System.IO;
using DbMaker;
using GreetingsWeb;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TransportMaker;

var host = CreateHostBuilder(args).Build();

host.CheckDbIsUp(ApplicationType.Greetings);
host.MigrateDatabase();
host.CreateOutbox(ApplicationType.Greetings, "Greetings", ConfigureTransport.HasBinaryMessagePayload());

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
            webBuilder.ConfigureLogging((hostingContext, logging) =>
            {
                logging.AddConsole();
                logging.AddDebug();
            });
            webBuilder.UseDefaultServiceProvider((context, options) =>
            {
                var isDevelopment = context.HostingEnvironment.IsDevelopment();
                options.ValidateScopes = isDevelopment;
                options.ValidateOnBuild = isDevelopment;
            });
            webBuilder.UseStartup<Startup>();
        });

