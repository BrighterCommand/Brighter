using System;
using System.Threading;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor;

namespace HelloAsyncListeners
{
    public class WantToBeGreeted : RequestHandlerAsync<GreetingEvent>
    {
        public override async Task<GreetingEvent> HandleAsync(GreetingEvent @event, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Simulated exceptions
            if (@event.Name.ToLower().Equals("roger"))
            {
                throw new ArgumentException("Roger's greeting will not be handled by Want-to-be-greeted.");
            }
            if (@event.Name.ToLower().Equals("devil"))
            {
                throw new ArgumentException("Devil's greeting will not be handled by Want-to-be-greeted.");
            }

            var api = new IpFyApi(new Uri("https://api.ipify.org"));
            var result = await api.GetAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

            Console.WriteLine("Want-to-be-greeted received hello from {0}", @event.Name);
            Console.WriteLine(result.Success ? "Want-to-be-greeted has public IP addres is {0}" : "Call to IpFy API failed : {0}",
                result.Message);

            return await base.HandleAsync(@event, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }
    }
}
