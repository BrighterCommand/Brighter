using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.Extensions.DependencyInjection;
using Serilog;

namespace HelloAsyncListeners
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>

                    {
                        services.AddBrighter().AutoFromAssemblies();
                        services.AddHostedService<RunCommandProcessor>();
                    }
                )
                .UseSerilog()
                .UseConsoleLifetime()
                .Build();

            await host.RunAsync();
        }
    }
}
