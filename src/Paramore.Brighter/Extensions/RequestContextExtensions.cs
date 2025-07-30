using System.Collections.Generic;

namespace Paramore.Brighter.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IRequestContext"/> to simplify access to common context bag values.
/// </summary>
/// <remarks>
/// These extensions offer type-safe access to Brighter's reserved context bag values,
/// handling type conversion and null checks internally. They are safe to call with null contexts.
/// </remarks>
public static class RequestContextExtensions
{
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
}
