using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace ParamoreBrighter.TickerQ.Tests.TestDoubles
{
    public class MyApplicationBuilder : IApplicationBuilder
    {
        public IServiceProvider ApplicationServices { get ; set ; }

        public IFeatureCollection ServerFeatures => throw new NotImplementedException();

        public IDictionary<string, object> Properties => throw new NotImplementedException();

        public RequestDelegate Build()
        {
            throw new NotImplementedException();
        }

        public IApplicationBuilder New()
        {
            throw new NotImplementedException();
        }

        public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware)
        {
            throw new NotImplementedException();
        }
    }
}
