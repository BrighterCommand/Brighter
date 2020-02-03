#region Licence

/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HelloWorldAsync
{
    internal struct IpFyApiResult
    {
        public IpFyApiResult(bool success, string message) : this()
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; private set; }
        public string Message { get; private set; }
    }

    internal class IpFyApi
    {
        private readonly Uri _endpoint;

        public IpFyApi(Uri endpoint)
        {
            _endpoint = endpoint;
        }

        public async Task<IpFyApiResult> GetAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = _endpoint;
                client.DefaultRequestHeaders.Clear();

                var response = await client.GetAsync("", cancellationToken);
                string result;
                if (response.IsSuccessStatusCode)
                    result = await response.Content.ReadAsStringAsync();
                else
                    result = "API returned HTTP status " + response.StatusCode;
                return new IpFyApiResult(response.IsSuccessStatusCode, result);
            }
        }
    }
}
