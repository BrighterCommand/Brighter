using Npgsql;
using Polly;

namespace GreetingsWeb.Extensions
{
    public static class DatabaseExtensions
    {
        public static IHost CheckDbIsUp(this IHost webHost)
        {
            using var scope = webHost.Services.CreateScope();

            var services = scope.ServiceProvider;
            var env = services.GetService<IWebHostEnvironment>();
            var config = services.GetService<IConfiguration>();
            string connectionString = config.GetConnectionString("MartenDb");

            WaitToConnect(connectionString);
            //TODO:  create db if not exists.

            return webHost;
        }

        private static void WaitToConnect(string connectionString)
        {
            var policy = Policy.Handle<NpgsqlException>().WaitAndRetryForever(
                retryAttempt => TimeSpan.FromSeconds(2),
                (exception, timespan) => { Console.WriteLine($"Healthcheck: Waiting for the database {connectionString} to come online - {exception.Message}"); });

            policy.Execute(() =>
            {
                using var conn = new NpgsqlConnection(connectionString);
                conn.Open();
            });
        }
    }
}
