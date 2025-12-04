using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using TickerQ.DependencyInjection;

namespace Paramore.Brighter.TickerQ.Tests.TestDoubles
{
    public static class TickerQModuleInitializer
    {
        private static bool _hasRun = false;
        private static readonly Lock _lockObject = new();
        public static void EnsureOneTimeSetupTickerQ(IApplicationBuilder appBuilder)
        {
            lock (_lockObject)
            {
                if (!_hasRun)
                {
                    appBuilder.UseTickerQ();
                    _hasRun = true;
                }
            }
        }
    }
}
