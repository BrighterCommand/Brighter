using System.Net.Http;
using Amazon.Runtime;

namespace Paramore.Brighter.AWS.Tests.Helpers;

internal class InterceptingHttpClientFactory(InterceptingDelegatingHandler handler) : HttpClientFactory
{
    public override HttpClient CreateHttpClient(IClientConfig clientConfig)
    {
        handler.InnerHandler ??= new HttpClientHandler();
        return new HttpClient(handler);
    }
}
