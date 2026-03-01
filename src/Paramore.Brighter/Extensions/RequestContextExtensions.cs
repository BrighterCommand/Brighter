#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.Extensions;

public static class RequestContextExtensions
{
    /// <summary>
    /// Retrieves CloudEvent additional properties from the request context bag.
    /// </summary>
    /// <param name="context">The request context (may be null)</param>
    /// <returns>
    /// The CloudEvent extensions dictionary if present and valid, otherwise null.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The value must be stored in the context bag as a <see cref="Dictionary{TKey, TValue}"/> 
    /// where TKey is <see cref="string"/> and TValue is <see cref="object"/>.
    /// </para>
    /// <para>
    /// Returns null for:
    /// <list type="bullet">
    /// <item><description>Null context</description></item>
    /// <item><description>Missing CloudEventsAdditionalProperties entry</description></item>
    /// <item><description>Type mismatch (not Dictionary&lt;string, object&gt;)</description></item>
    /// </list>
    /// </para>
    /// <example>
    /// Usage:
    /// <code>
    /// var cloudEventProps = requestContext.GetCloudEventAdditionalProperties();
    /// if (cloudEventProps != null)
    /// {
    ///     // Add to CloudEvent extensions
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="RequestContextBagNames.CloudEventsAdditionalProperties"/>
    /// </remarks>
    public static Dictionary<string, object>? GetCloudEventAdditionalProperties(this IRequestContext? context)
    {
        if (context != null 
            && context.Bag.TryGetValue(RequestContextBagNames.CloudEventsAdditionalProperties, out var tmp)
            && tmp is Dictionary<string, object> cloudEventAdditionalProperties)
        {
            return cloudEventAdditionalProperties;
        }

        return null;
    }

    /// <summary>
    /// Retrieves the JobId value from the request context bag.
    /// </summary>
    /// <param name="context">The request context (may be null).</param>
    /// <returns>
    /// The JobId as an <see cref="Id"/> if present and valid; otherwise, an empty <see cref="Id"/>.
    /// </returns>
    /// <remarks>
    /// Reserved for future usage, this method retrieves the JobId which represents an instance of a workflow.
    /// Returns an empty <see cref="Id"/> in the following cases:
    /// - Null context.
    /// - Missing JobId entry in the context bag.
    /// - Type mismatch (not of type <see cref="Id"/>).
    /// </remarks>
    public static Id? GetJobId(this IRequestContext? context)
    {
        if (context != null
            && context.Bag.TryGetValue(RequestContextBagNames.JobId, out var tmp) 
            && tmp is Id jobId)
        {
            return jobId;
        }

        return null;
    }
    
    /// <summary>
    /// Retrieves the partition key from the request context bag.
    /// </summary>
    /// <param name="context">The request context (may be null)</param>
    /// <returns>
    /// The partition key if present and valid, otherwise <see cref="PartitionKey.Empty"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Handles two valid value types in the context bag:
    /// <list type="bullet">
    /// <item><description><see cref="string"/>: Converted to a <see cref="PartitionKey"/> instance</description></item>
    /// <item><description><see cref="PartitionKey"/>: Returned directly</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Returns <see cref="PartitionKey.Empty"/> for:
    /// <list type="bullet">
    /// <item><description>Null context</description></item>
    /// <item><description>Missing partition key entry</description></item>
    /// <item><description>Unsupported value types</description></item>
    /// </list>
    /// </para>
    /// <example>
    /// Usage:
    /// <code>
    /// var partitionKey = requestContext.GetPartitionKey();
    /// if (!partitionKey.IsEmpty)
    /// {
    ///     // Use partition key
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    public static PartitionKey GetPartitionKey(this IRequestContext? context)
    {
        if (context == null || !context.Bag.TryGetValue(RequestContextBagNames.PartitionKey, out var tmp))
        {
            return PartitionKey.Empty;
        }

        return tmp switch
        {
            string partitionKeyAsString => new PartitionKey(partitionKeyAsString),
            PartitionKey partitionKey => partitionKey,
            _ => PartitionKey.Empty
        };
    }

    /// <summary>
    /// Retrieves the dynamic headers dictionary from the request context bag.
    /// </summary>
    /// <param name="context">The request context (may be null)</param>
    /// <returns>
    /// The headers dictionary if present and valid, otherwise null.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The value must be stored in the context bag as a <see cref="Dictionary{TKey, TValue}"/> 
    /// where TKey is <see cref="string"/> and TValue is <see cref="object"/>.
    /// </para>
    /// <para>
    /// Returns null for:
    /// <list type="bullet">
    /// <item><description>Null context</description></item>
    /// <item><description>Missing headers entry</description></item>
    /// <item><description>Type mismatch (not Dictionary&lt;string, object&gt;)</description></item>
    /// </list>
    /// </para>
    /// <example>
    /// Usage:
    /// <code>
    /// var headers = requestContext.GetHeaders();
    /// if (headers != null)
    /// {
    ///     foreach (var header in headers)
    ///     {
    ///         // Process headers
    ///     }
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    public static Dictionary<string, object>? GetHeaders(this IRequestContext? context)
    {
        if (context != null 
            && context.Bag.TryGetValue(RequestContextBagNames.Headers, out var tmp)
            && tmp is Dictionary<string, object> headers)
        {
            return headers;
        }

        return null;
    }

    /// <summary>
    /// Retrieves the WorkflowId value from the request context bag.
    /// </summary>
    /// <param name="context">The request context (may be null).</param>
    /// <returns>
    /// The WorkflowId as an <see cref="Id"/> if present and valid, otherwise null.
    /// </returns>
    /// <remarks>
    /// Retrieves the WorkflowId which represents an instance of a workflow.
    /// Returns null in the following cases:
    /// - The context is null.
    /// - The WorkflowId key is missing in the context bag.
    /// - The value associated with the WorkflowId key is not of type <see cref="Id"/>.
    /// </remarks>
    public static Id? GetWorkflowId(this IRequestContext? context)
    {
        if (context != null
            && context.Bag.TryGetValue(RequestContextBagNames.WorkflowId, out var tmp) 
            && tmp is Id workflowId)
        {
            return workflowId;
        }

        return null;
    }
}
