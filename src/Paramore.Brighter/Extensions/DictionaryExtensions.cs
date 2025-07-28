using System.Collections.Generic;

namespace Paramore.Brighter.Extensions;

/// <summary>
/// Provides extension methods for dictionaries.
/// </summary>
public static class DictionaryExtensions
{
    /// <summary>
    /// Merges two dictionaries into a new dictionary, with values from the second dictionary overwriting those from the first.
    /// </summary>
    /// <param name="dict1">The primary dictionary to merge (will be copied first)</param>
    /// <param name="dict2">The secondary dictionary to merge (may be null)</param>
    /// <returns>
    /// A new dictionary containing the combined key-value pairs from both dictionaries.
    /// When keys exist in both dictionaries, the value from <paramref name="dict2"/> takes precedence.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method performs a shallow merge where:
    /// <list type="number">
    /// <item><description>All entries from <paramref name="dict1"/> are copied to the new dictionary</description></item>
    /// <item><description>Entries from <paramref name="dict2"/> are then added or overwrite existing keys</description></item>
    /// <item><description>Null values in <paramref name="dict2"/> will overwrite existing keys with null</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Special cases:
    /// <list type="bullet">
    /// <item><description>If <paramref name="dict2"/> is null, returns a copy of <paramref name="dict1"/></description></item>
    /// <item><description>Original dictionaries remain unmodified</description></item>
    /// <item><description>Performs a shallow copy (values are not cloned)</description></item>
    /// </list>
    /// </para>
    /// <example>
    /// Basic merge with key override:
    /// <code>
    /// var dict1 = new Dictionary&lt;string, object&gt; { ["a"] = 1, ["b"] = 2 };
    /// var dict2 = new Dictionary&lt;string, object&gt; { ["b"] = 99, ["c"] = 3 };
    /// 
    /// var merged = dict1.Merge(dict2);
    /// // Result: { "a": 1, "b": 99, "c": 3 }
    /// </code>
    /// 
    /// Handling null merge:
    /// <code>
    /// var merged = dict1.Merge(null);
    /// // Returns copy of dict1
    /// </code>
    /// </example>
    /// </remarks>
    public static Dictionary<string, object> Merge(this IDictionary<string, object> dict1,
        IDictionary<string, object>? dict2)
    {
        var result = new Dictionary<string, object>(dict1);
        if(dict2  != null)
        {
            foreach (var val in dict2)
            {
                result[val.Key] = val.Value;
            }
        }
        
        return result;
    }
}
