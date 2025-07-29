using System;

namespace Paramore.Brighter;

/// <summary>
/// Provides GUID generation methods with version-specific implementations.
/// On .NET 9+ uses time-ordered GUIDv7 format, otherwise uses standard GUIDv4 format.
/// </summary>
/// <remarks>
/// <para>
/// This class offers improved collision characteristics and database index efficiency
/// when running on .NET 9 or later by using GUIDv7 format. The implementation falls back
/// to standard <see cref="Guid.NewGuid"/> for earlier .NET versions.
/// </para>
/// <para>
/// GUIDv7 characteristics (when available):
/// <list type="bullet">
///   <item><description>Time-ordered values for better database index locality</description></item>
///   <item><description>48-bit Unix timestamp with millisecond precision</description></item>
///   <item><description>Improved collision resistance compared to v4</description></item>
///   <item><description>IETF draft standard: https://datatracker.ietf.org/doc/draft-ietf-uuidrev-rfc4122bis/</description></item>
/// </list>
/// </para>
/// </remarks>
public static class Uuid
{
    /// <summary>
    /// Generates a new GUID using the optimal version for the current runtime.
    /// </summary>
    /// <returns>
    /// <list type="bullet">
    ///   <item><description>.NET 9+: GUIDv7 (time-ordered)</description></item>
    ///   <item><description>Earlier versions: Standard GUIDv4</description></item>
    /// </list>
    /// </returns>
    /// <example>
    /// <code>
    /// var id = Uuid.New(); // Optimal GUID for current runtime
    /// </code>
    /// </example>
    public static Guid New()
    {
#if NET9_0_OR_GREATER
        return Guid.CreateVersion7();
#else 
        return Guid.NewGuid();
#endif
    }

    /// <summary>
    /// Generates a new GUID string using the optimal version for the current runtime.
    /// </summary>
    /// <returns>
    /// String representation of:
    /// <list type="bullet">
    ///   <item><description>.NET 9+: GUIDv7 (time-ordered)</description></item>
    ///   <item><description>Earlier versions: Standard GUIDv4</description></item>
    /// </list>
    /// </returns>
    /// <example>
    /// <code>
    /// string id = Uuid.NewAsString(); // "xxxxxxxx-xxxx-7xxx-xxxx-xxxxxxxxxxxx"
    /// </code>
    /// </example>
    public static string NewAsString()
    {
#if NET9_0_OR_GREATER
        return Guid.CreateVersion7().ToString();
#else 
        return Guid.NewGuid().ToString();
#endif
    }
}
