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
using System.Net.Http;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;
using Paramore.Brighter.Transforms.Storage;
using Polly.Retry;

namespace Paramore.Brighter.Transformers.AWS;

/// <summary>
/// Configuration Options for an S3 backed luggage store
/// </summary>
public class S3LuggageOptions : StorageOptions
{
    public const string DefaultBucketAddressTemplate = "https://{BucketName}.s3.{BucketRegion}.amazonaws.com";
    
    private static readonly Regex s_validBucketNameRx = new("(?!(^xn--|.+-s3alias$))^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$", RegexOptions.Compiled);
    
    public S3LuggageOptions(AWSS3Connection connection, string bucketName)
    {
        if (connection == null)
        {
            throw new ArgumentNullException(nameof(connection));
        }
        
        BucketName = bucketName;
        
        var s3Config = new AmazonS3Config { RegionEndpoint = connection.Region };
        connection.ClientConfig?.Invoke(s3Config);
        Client = new AmazonS3Client(connection.Credentials, s3Config);

        var stsConfig = new AmazonSecurityTokenServiceConfig { RegionEndpoint = connection.Region };
        connection.ClientConfig?.Invoke(stsConfig);
        
        StsClient = new AmazonSecurityTokenServiceClient(connection.Credentials, stsConfig);
        BucketRegion = new S3Region(connection.Region.SystemName);
    }
    
    /// <summary>
    /// How should we control access to the bucket used by the Luggage Store 
    /// </summary>
    public S3CannedACL? ACLs { get; set; }

    private string _bucketName = string.Empty;

    /// <summary>
    /// The name of the bucket, which will need to be unique within the AWS region
    /// </summary>
    public string BucketName
    {
        get => _bucketName;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(nameof(value), "The bucketName can't be empty");
            }
            
            if (!s_validBucketNameRx.IsMatch(value))
            {
                throw new ArgumentException("The bucketName does not match S3 naming rules");
            }

            _bucketName = value;
        }
    }

    public string LuggagePrefix { get; set; } = "BRIGHTER_CHECKED_LUGGAGE";

    /// <summary>
    /// The AWS region to create the bucket in
    /// </summary>
    public S3Region BucketRegion { get; set; }

    /// <summary>
    /// Get the AWS S3 Client
    /// </summary>
    public IAmazonS3 Client { get; }

    /// <summary>
    /// An HTTP client factory. We use this to grab an HTTP client, so that we can check if the bucket exists.
    /// Not required if you choose a <see cref="StorageOptions.Strategy"/> of <see cref="StorageStrategy.Assume"/> Exists.
    /// We obtain this from the ServiceProvider when constructing the luggage store. so you do not need to set it
    /// </summary>
    public IHttpClientFactory? HttpClientFactory { get; set; }

    /// <summary>
    /// The Security Token Service created from the credentials. Used to obtain the account id of the user with those credentials
    /// </summary>
    public IAmazonSecurityTokenService StsClient { get; }

    /// <summary>
    /// Tags for the bucket. Defaults to a Creator tag of "Brighter Luggage Store"
    /// </summary>
    public List<Tag> Tags { get; set; } = [new Tag { Key = "Creator", Value = " Brighter Luggage Store" }];

    /// <summary>
    /// How long to keep aborted uploads before deleting them in days
    /// </summary>
    public int TimeToAbortFailedUploads { get; set; } = 1;

    /// <summary>
    /// How long to keep good uploads in days, before deleting them
    /// </summary>
    public int TimeToDeleteGoodUploads { get; set; } = 7;

    /// <summary>
    /// The bucket address template
    /// </summary>
    /// <remarks>
    /// If you are using an S3 compatible storage provided,
    /// please update this endpoint if you want to Brighter to create your bucket
    /// </remarks>
    public string BucketAddressTemplate { get; set; } = DefaultBucketAddressTemplate;
    
    public AsyncRetryPolicy? RetryPolicy { get; set; }
}
