#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Confluent.SchemaRegistry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Paramore.Brighter.SchemaRegistry.Confluent
{
    public class SchemaRegistryService
    {
        private readonly List<SchemaReference> EmptyReferencesList = new List<SchemaReference>();

        private static readonly string acceptHeader = string.Join(", ", Versions.PreferredResponseTypes);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly int _timeoutMs;
        private readonly Uri _uri;

        private readonly IAuthenticationHeaderValueProvider authenticationHeaderValueProvider;


        /// <summary>
        /// Abstracts access to the Confluent Schema Registry API. Although we can use the Confluent CachedSchemaRegistryClient which encapsulates the RestService
        /// for a simple Register and Get model, it does not provide other convenient features
        /// <param name="httpClientFactory">The HTTP Client Factory used to create HTTP client instances</param>
        /// <param name="schemaRegistryUrl">The URI for the schema registry </param>
        /// <param name="timeoutMS">The timeout for the network call in Ms</param>
        /// <param name="authenticationHeaderValueProvider"> An interface defining HTTP client authentication header values.</param>
        /// <param name="certificates">A collection of X509 certificates</param>
        /// </summary>
        public SchemaRegistryService(
            IHttpClientFactory httpClientFactory,
            string schemaRegistryUrl,
            int timeoutMS = 250,
            IAuthenticationHeaderValueProvider authenticationHeaderValueProvider = null, 
            List<X509Certificate2> certificates = null, 
            bool enableSslCertificateVerification = false)
        {
            _httpClientFactory = httpClientFactory;
            _timeoutMs = timeoutMS;
            _uri = new Uri(schemaRegistryUrl, UriKind.Absolute);
            this.authenticationHeaderValueProvider = authenticationHeaderValueProvider;


            /*
            this.clients = schemaRegistryUrl
                .Split(',')
                .Select(SanitizeUri)// need http or https - use http if not present.
                .Select(uri =>
                {
                    HttpClient client;
                    if (certificates.Count > 0)
                    {
                        client = new HttpClient(CreateHandler(certificates, enableSslCertificateVerification)) { BaseAddress = new Uri(uri, UriKind.Absolute), Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
                    }
                    else
                    {
                        client = new HttpClient() { BaseAddress = new Uri(uri, UriKind.Absolute), Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
                    }
                    return client;
                })
                .ToList();
                */
        }

        private HttpClient GetClient()
        {
            HttpClient client;
            client = _httpClientFactory.CreateClient();
            client.BaseAddress = _uri ;
            client.Timeout = TimeSpan.FromMilliseconds(_timeoutMs);
        }

        private static HttpClientHandler CreateHandler(List<X509Certificate2> certificates, bool enableSslCertificateVerification)
        {
            var handler = new HttpClientHandler();
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;

            if (!enableSslCertificateVerification)
            {
                handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, certChain, policyErrors) => { return true; };
            }

            certificates.ForEach(c => handler.ClientCertificates.Add(c));
            return handler;
        }

        /// <remarks>
        ///     Used for end points that return a json object { ... }
        /// </remarks>
        private async Task<T> RequestAsync<T>(string endPoint, HttpMethod method, params object[] jsonBody)
        {
            var response = await ExecuteOnOneInstanceAsync(() => CreateRequest(endPoint, method, jsonBody)).ConfigureAwait(continueOnCapturedContext: false);
            string responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
            T t = JObject.Parse(responseJson).ToObject<T>();
            return t;
        }

        /// <remarks>
        ///     Used for end points that return a json array [ ... ]
        /// </remarks>
        private async Task<List<T>> RequestListOfAsync<T>(string endPoint, HttpMethod method, params object[] jsonBody)
        {
            var response = await ExecuteOnOneInstanceAsync(() => CreateRequest(endPoint, method, jsonBody))
                                    .ConfigureAwait(continueOnCapturedContext: false);
            return JArray.Parse(
                await response.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false)).ToObject<List<T>>();
        }

        private HttpRequestMessage CreateRequest(string endPoint, HttpMethod method, params object[] jsonBody)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, endPoint);
            request.Headers.Add("Accept", acceptHeader);
            if (jsonBody.Length != 0)
            {
                string stringContent = string.Join("\n", jsonBody.Select(x => JsonConvert.SerializeObject(x)));
                var content = new StringContent(stringContent, System.Text.Encoding.UTF8, Versions.SchemaRegistry_V1_JSON);
                content.Headers.ContentType.CharSet = string.Empty;
                request.Content = content;
            }
            if (authenticationHeaderValueProvider != null)
            {
                request.Headers.Authorization = authenticationHeaderValueProvider.GetAuthenticationHeader();
            }
            return request;
        }
  
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

    }
}
