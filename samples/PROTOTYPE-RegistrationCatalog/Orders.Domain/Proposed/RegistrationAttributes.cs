// ─────────────────────────────────────────────────────────────────────────────────────
// PROTOTYPE — THROWAWAY. In the real design these are emitted internal-per-assembly by the
// generator's post-init output (like the PR's existing BrighterRegistrationsAttribute).
// ─────────────────────────────────────────────────────────────────────────────────────
using System;

namespace Paramore.Brighter;

/// <summary>Marks a static partial class the generator fills with a RegistrationCatalog.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class BrighterRegistrationsAttribute : Attribute
{
    /// <summary>
    /// Phase 2: also emit one-line fluent extensions — Add{HolderName}() for the whole catalog and
    /// Add{GroupName}Registrations() per [RegistrationGroup] — each delegating to AddRegistrations.
    /// DELIBERATE TRADE: the extensions take IBrighterBuilder, so opting in couples the declaring
    /// assembly to the DI package. Setting this without referencing it is a build-error diagnostic.
    /// </summary>
    public bool GenerateBuilderExtensions { get; set; }
}

/// <summary>
/// Declares a named sub-catalog scooped by convention — the Brighter analogue of NServiceBus's
/// RegistrationMethodNamePatterns. The generator evaluates the convention AT BUILD TIME against
/// the discovered types and emits the group as a pre-filtered static catalog property named
/// <see cref="Name"/>. An invalid regex or a group that matches nothing is a build-time
/// diagnostic, not a runtime surprise.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RegistrationGroupAttribute(string name) : Attribute
{
    /// <summary>Generated property name; must be a valid C# identifier.</summary>
    public string Name { get; } = name;

    /// <summary>Scoop every discovered type in this namespace (and below).</summary>
    public string? InNamespace { get; set; }

    /// <summary>Scoop every discovered type whose simple name matches this regex.</summary>
    public string? TypeNamePattern { get; set; }
}
