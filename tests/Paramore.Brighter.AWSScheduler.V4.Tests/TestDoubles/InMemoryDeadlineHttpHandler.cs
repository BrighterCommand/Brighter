#region Licence
/* The MIT License (MIT)
Copyright © 2026 gabisonia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

#nullable enable

using System.Net;
using System.Text;
using System.Text.Json;

namespace Paramore.Brighter.AWSScheduler.V4.Tests.TestDoubles;

internal sealed class InMemoryDeadlineHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> _schedules = new();

    public List<(HttpMethod Method, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = request.RequestUri!.AbsolutePath;
        if (!path.StartsWith("/schedules/", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected scheduler endpoint: {path}");

        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, body));

        if (request.Method == HttpMethod.Get)
            return JsonResponse(_schedules[path]);

        if (request.Method != HttpMethod.Post && request.Method != HttpMethod.Put)
            throw new InvalidOperationException($"Unexpected scheduler method: {request.Method}");

        _schedules[path] = body;
        return JsonResponse(JsonSerializer.Serialize(new
        {
            ScheduleArn = "arn:aws:scheduler:us-east-1:123456789012:schedule/default/deadline-id"
        }));
    }

    private static HttpResponseMessage JsonResponse(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
}
