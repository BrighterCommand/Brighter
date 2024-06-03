using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.AWS.Tests.Helpers
{
    internal class InterceptingDelegatingHandler : DelegatingHandler
    {
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
