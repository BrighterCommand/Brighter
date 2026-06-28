#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Paramore.Brighter.SourceGenerators.Model;

/// <summary>
/// Per-method projection produced by the syntax-provider transform. Holds only value-equatable
/// data so the incremental pipeline can cache it across runs.
/// </summary>
public sealed record MethodTarget(
    string? Namespace,
    string ContainingTypeAccessibility,
    string ContainingTypeName,
    bool ContainingTypeIsStatic,
    string MethodAccessibility,
    string MethodName,
    string ReturnTypeFullyQualified,
    string ParameterTypeFullyQualified,
    string ParameterName,
    bool IsExtensionMethod,
    string HintName,
    bool IsPartial = true);

/// <summary>The category of a discovered Brighter registration candidate.</summary>
public enum DiscoveredKind
{
    SyncHandler,
    AsyncHandler,
    Mapper,
    AsyncMapper,
    Transform,
}

/// <summary>
/// One discovered registration candidate. Multiple entries may originate from the same class
/// (e.g., a class implementing both a sync and async handler interface).
/// </summary>
public sealed record DiscoveredEntry(
    DiscoveredKind Kind,
    string RequestTypeFullyQualified,
    string TypeFullyQualified,
    bool IsOpenGeneric);

/// <summary>
/// Value-equatable snapshot of a Roslyn <see cref="Microsoft.CodeAnalysis.Location"/>. Carried
/// through the pipeline so a diagnostic can be rebuilt at source-output time without holding
/// onto non-cacheable Roslyn objects.
/// </summary>
public sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo? From(Location? location)
    {
        if (location is null || !location.IsInSource)
            return null;
        var lineSpan = location.GetLineSpan();
        return new LocationInfo(lineSpan.Path, location.SourceSpan, lineSpan.Span);
    }
}

/// <summary>
/// Deferred diagnostic representation. Created in the syntax-provider transform; converted back
/// to <see cref="Diagnostic"/> at source-output time.
/// </summary>
public sealed record DiagnosticInfo(string Id, LocationInfo? Location, string Argument);

/// <summary>
/// Result of reading a single <c>[BrighterRegistrations]</c>-attributed method: either a valid
/// <see cref="MethodTarget"/>, or a diagnostic describing why the method was rejected.
/// </summary>
public sealed record MethodCandidate(MethodTarget? Method, DiagnosticInfo? Diagnostic);

/// <summary>
/// Per-class output from the discovery transform: zero or more registration entries, plus zero
/// or more diagnostics (e.g. <c>BRGEN005</c> when a generic mapper/transform is observed).
/// </summary>
public sealed record DiscoveryBatch(
    EquatableArray<DiscoveredEntry> Entries,
    EquatableArray<DiagnosticInfo> Diagnostics)
{
    public static readonly DiscoveryBatch Empty = new(EquatableArray<DiscoveredEntry>.Empty, EquatableArray<DiagnosticInfo>.Empty);

    public bool IsEmpty => Entries.Count == 0 && Diagnostics.Count == 0;
}
