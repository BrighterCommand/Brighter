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

using System.Collections.Generic;
using System.Net.Http;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;

namespace Paramore.Brighter.Tranformers.AWS
{
    /// <summary>
    /// Configuration Options for an S3 backed luggage store
    /// </summary>
    public class S3LuggageOptions
    {
        /// <summary>
        /// Construct an S3 based Luggage Store for large message payloads
        /// Defaults:
        /// StoreCreation: Create if Missing
        /// ACLs: Private (Owner has full access, no one else has access)
        /// Tags: Key: Creator Value: Brighter Luggage Store
        /// TimeToAbortFailedUpdates = 1 day
        /// TimeToDeleteGoodUploads = 7 days
        /// </summary>
        public S3LuggageOptions(IHttpClientFactory httpClientFactory)
        {
            StoreCreation = S3LuggageStoreCreation.CreateIfMissing;
            ACLs = S3CannedACL.Private;
            HttpClientFactory = httpClientFactory;
            Tags = new List<Tag> { new Tag { Key = "Creator", Value = " Brighter Luggage Store" } };
            TimeToAbortFailedUploads = 1;
            TimeToDeleteGoodUploads = 7;
        }

        /// <summary>
        /// How should we control access to the bucket used by the Luggage Store 
        /// </summary>
        public S3CannedACL ACLs { get; set; }

        /// <summary>
        /// The credentials for the user to create the client from - it will be the bucket owner
        /// </summary>
        public AWSS3Connection Connection
        {
            set
            {
                var s3Config = new AmazonS3Config { RegionEndpoint = value.Region };
                value.ClientConfig?.Invoke(s3Config);
                Client = new AmazonS3Client(value.Credentials, s3Config);

                var stsConfig = new AmazonSecurityTokenServiceConfig { RegionEndpoint = value.Region };
                value.ClientConfig?.Invoke(stsConfig);
                StsClient = new AmazonSecurityTokenServiceClient(value.Credentials, stsConfig);
            }
        }

        /// <summary>
        /// The name of the bucket, which will need to be unique within the AWS region
        /// </summary>
        public string BucketName { get; set; }

        /// <summary>
        /// The AWS region to create the bucket in
        /// </summary>
        public S3Region BucketRegion { get; set; }

        /// <summary>
        /// Get the AWS client created from the credentials passed into <see cref="Connection"/>
        /// </summary>
        public IAmazonS3 Client { get; private set; }

        /// <summary>
        /// An HTTP client factory. We use this to grab an HTTP client, so that we can check if the bucket exists.
        /// Not required if you choose a <see cref="StoreCreation"/> of Assume Exists.
        /// We obtain this from the ServiceProvider when constructing the luggage store. so you do not need to set it
        /// </summary>
        public IHttpClientFactory HttpClientFactory { get; private set; }

        /// <summary>
        /// What Store Creation Option do you want:
        /// 1: Create
        /// 2: Validate
        /// 3: Assume it exists
        /// </summary>
        public S3LuggageStoreCreation StoreCreation { get; set; }

        /// <summary>
        /// The Security Token Service created from the credentials. Used to obtain the account id of the user with those credentials
        /// </summary>
        public IAmazonSecurityTokenService StsClient { get; private set; }

        /// <summary>
        /// Tags for the bucket. Defaults to a Creator tag of "Brighter Luggage Store"
        /// </summary>
        public List<Tag> Tags { get; set; }

        /// <summary>
        /// How long to keep aborted uploads before deleting them in days
        /// </summary>
        public int TimeToAbortFailedUploads { get; set; }

        /// <summary>
        /// How long to keep good uploads in days, before deleting them
        /// </summary>
        public int TimeToDeleteGoodUploads { get; set; }
    }
}
