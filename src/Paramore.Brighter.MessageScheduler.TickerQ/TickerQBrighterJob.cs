using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Scheduler.Events;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Paramore.Brighter.MessageScheduler.TickerQ
{
    /// <summary>
    /// The TickerQ Message scheduler Job
    /// </summary>
    /// <param name="processor"></param>
    public class BrighterTickerQSchedulerJob(IAmACommandProcessor processor)
    {
        [TickerFunction(nameof(FireSchedulerMessageAsync))]
        public async Task FireSchedulerMessageAsync(TickerFunctionContext<string> tickerContext, CancellationToken cancellationToken)
        {
            var schedulerRequest = JsonSerializer.Deserialize<FireSchedulerMessage>(tickerContext.Request, JsonSerialisationOptions.Options)!;
            await processor.SendAsync(schedulerRequest);
        }

        [TickerFunction(nameof(FireSchedulerRequestAsync))]
        public async Task FireSchedulerRequestAsync(TickerFunctionContext<string> tickerContext, CancellationToken cancellationToken)
        {
            try
            {
                //    var schedulerRequest = JsonSerializer.Deserialize<FireSchedulerRequest>(tickerContext.Request, JsonSerialisationOptions.Options)!;
                // await processor.SendAsync(schedulerRequest);
                await processor.SendAsync(new FireSchedulerRequest());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }

        }
    }
}
