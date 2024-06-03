using System.Net.Http;
using Amazon.Runtime;

namespace Paramore.Brighter.AWS.Tests.Helpers
{
    internal class InterceptingHttpClientFactory : HttpClientFactory
    {
        private readonly InterceptingDelegatingHandler _handler;

        public InterceptingHttpClientFactory(InterceptingDelegatingHandler handler)
        {
            _handler = handler;
        }

        public override HttpClient CreateHttpClient(IClientConfig clientConfig)
        {
            return new HttpClient(_handler);
        }
    }
}
