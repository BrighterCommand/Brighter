using System.Diagnostics.CodeAnalysis;

namespace Paramore.Brighter.Transformers.JustSaying;

/// <summary>
/// Represents a tenant identifier in a multi-tenant system.
/// </summary>
/// <remarks>
/// This value type provides type safety for tenant identifiers while maintaining
/// seamless interoperability with strings through implicit conversions.
/// </remarks>
public readonly record struct Tenant(string Value)
{
    /// <summary>
    /// Gets an empty tenant identifier.
    /// </summary>
    /// <value>A <see cref="Tenant"/> instance with an empty string value.</value>
    public static Tenant Empty {get;} = new(string.Empty);
    
    /// <summary>
    /// Determines whether the specified tenant is null or contains an empty value.
    /// </summary>
    /// <param name="tenant">The nullable tenant to test.</param>
    /// <returns>
    /// <c>true</c> if the <paramref name="tenant"/> parameter is null or its value is empty; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsNullOrEmpty([NotNullWhen(false)]Tenant? tenant) => tenant is null || string.IsNullOrEmpty(tenant.Value.Value);

    /// <summary>
    /// Determines whether the specified tenant contains an empty value.
    /// </summary>
    /// <param name="tenant">The tenant to test (non-nullable).</param>
    /// <returns>
    /// <c>true</c> if the <paramref name="tenant"/>'s value is null or empty; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsNullOrEmpty(Tenant tenant) => string.IsNullOrEmpty(tenant.Value);

    /// <summary>
    /// Returns the string value of the tenant identifier.
    /// </summary>
    /// <returns>The string representation of the tenant identifier.</returns> 
    public override string ToString() => Value;

    /// <summary>
    /// Provides implicit conversion from <see cref="Tenant"/> to <see cref="string"/>.
    /// </summary>
    /// <param name="tenant">The tenant instance to convert.</param>
    public static implicit operator string(Tenant tenant) => tenant.Value;

    /// <summary>
    /// Provides implicit conversion from <see cref="string"/> to <see cref="Tenant"/>.
    /// </summary>
    /// <param name="value">The string value to convert to a tenant identifier.</param>
    public static implicit operator Tenant(string value) => new(value);
}
