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
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Tranformers.AWS
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Will add an S3 Luggage Store into the Service Collection
        /// Sync over async,  but alright as we are in startup
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configure"></param>
        public static void AddS3LuggageStore(this IServiceCollection services, Action<S3LuggageOptions> configure)
        {
            services.TryAddSingleton<IAmAStorageProviderAsync>(sp =>
            {
                var httpClientFactory = sp.GetService<IHttpClientFactory>();
               
                var options = new S3LuggageOptions(httpClientFactory);

                configure(options);

                 return S3LuggageStore.CreateAsync(
                        client: options.Client,
                        bucketName: options.BucketName,
                        storeCreation: options.StoreCreation,
                        httpClientFactory: options.HttpClientFactory,
                        stsClient: options.StsClient,
                        bucketRegion: options.BucketRegion,
                        tags: options.Tags,
                        acl: options.ACLs,
                        abortFailedUploadsAfterDays: options.TimeToAbortFailedUploads,
                        deleteGoodUploadsAfterDays: options.TimeToDeleteGoodUploads)
                    .GetAwaiter()
                    .GetResult();
            });
        }
    }
}
