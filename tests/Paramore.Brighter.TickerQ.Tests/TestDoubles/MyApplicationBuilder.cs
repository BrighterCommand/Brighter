using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Paramore.Brighter.TickerQ.Tests.TestDoubles
{
    public class MyApplicationBuilder : IApplicationBuilder
    {

        private readonly List<Func<RequestDelegate, RequestDelegate>> _components =
            new List<Func<RequestDelegate, RequestDelegate>>();

        public MyApplicationBuilder(IServiceProvider serviceProvider)
        {
            ApplicationServices = serviceProvider;
            Properties = new Dictionary<string, object?>();
            ServerFeatures = new FeatureCollection();
        }

        public IServiceProvider ApplicationServices { get; set; }

        public IDictionary<string, object?> Properties { get; set; }

        public IFeatureCollection ServerFeatures { get; }

        public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware)
        {

            return this;
        }

        public IApplicationBuilder New()
        {
            return new MyApplicationBuilder(ApplicationServices)
            {
                Properties = this.Properties,
                ApplicationServices = this.ApplicationServices
            };
        }

        public RequestDelegate Build()
        {
            RequestDelegate app = context =>
            {
                return context.Response.WriteAsync("Hello from MyApplicationBuilder");
            };


            return app;
        }
    }
}
