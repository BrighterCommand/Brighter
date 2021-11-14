using Greetings.Adaptors.Data;
using Greetings.Ports.Commands;
using Greetings.Ports.Events;
using Greetings.Ports.Mappers;
using GreetingsSender.Web.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.MsSql.EntityFrameworkCore;
using Paramore.Brighter.Outbox.MsSql;

namespace GreetingsSender.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //string dbConnString = "server=(localdb)\\mssqllocaldb;database=BrighterTests;trusted_connection=yes;MultipleActiveResultSets=True";
            string dbConnString = "Server=127.0.0.1,11433;Database=BrighterTests;User Id=sa;Password=Password1!;Application Name=BrighterTests";
            
            //EF
            services.AddDbContext<GreetingsDataContext>(o =>
            {
                o.UseSqlServer(dbConnString, b =>
                {
                    b.MigrationsAssembly("Greetings");
                });
            });
            
            //Brighter
            string asbFQDN = ".servicebus.windows.net";

            var asbClientProvider = new ServiceBusVisualStudioCredentialClientProvider(asbFQDN);
            var producer = AzureServiceBusMessageProducerFactory.Get(asbClientProvider);

            var outboxConfig = new MsSqlConfiguration(dbConnString, "BrighterOutbox");
            
            services
                .AddBrighter(opt =>
                {
                    opt.PolicyRegistry = new DefaultPolicy();
                    opt.CommandProcessorLifetime = ServiceLifetime.Scoped;
                })
                .UseExternalBus(producer)
                .UseMsSqlOutbox(outboxConfig, typeof(MsSqlSqlAuthConnectionProvider))
                //.UseMsSqlOutbox(outboxConfig, typeof(MsSqlEntityFrameworkCoreConnectionProvider<GreetingsDataContext>), ServiceLifetime.Scoped)
                .UseMsSqlTransactionConnectionProvider(typeof(MsSqlEntityFrameworkCoreConnectionProvider<GreetingsDataContext>))
                .MapperRegistry(r =>
                {
                    r.Add(typeof(GreetingEvent), typeof(GreetingEventMessageMapper));
                    r.Add(typeof(GreetingAsyncEvent), typeof(GreetingEventAsyncMessageMapper));
                    r.Add(typeof(AddGreetingCommand), typeof(AddGreetingMessageMapper));
                });


            services.AddControllersWithViews();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                
                using var serviceScope = app.ApplicationServices.CreateScope();
                var services = serviceScope.ServiceProvider;
                var dbContext = services.GetService<GreetingsDataContext>();

                dbContext.Database.Migrate();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
