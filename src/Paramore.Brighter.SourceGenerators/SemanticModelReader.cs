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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Paramore.Brighter.SourceGenerators.Model;

namespace Paramore.Brighter.SourceGenerators;

/// <summary>
/// Holds the two transform entry points that touch Roslyn semantic-model objects. Both produce
/// value-equatable records so the incremental pipeline can cache their output by value rather
/// than by symbol identity (which is never stable across compilations).
/// </summary>
public static class SemanticModelReader
{
    /// <summary>
    /// Transform for <see cref="IncrementalGeneratorInitializationContext.SyntaxProvider"/>
    /// <c>.ForAttributeWithMetadataName</c>: validates the attributed method and projects it
    /// to a Roslyn-free <see cref="MethodCandidate"/>.
    /// </summary>
    public static MethodCandidate ReadMethod(GeneratorAttributeSyntaxContext ctx, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ctx.TargetSymbol is not IMethodSymbol method)
            return new MethodCandidate(null, null);

        var markers = MarkerSymbols.Resolve(ctx.SemanticModel.Compilation);
        if (!markers.IsValid)
            return new MethodCandidate(null, null);

        var diagnostic = ValidateMethod(method, markers);
        if (diagnostic is not null)
            return new MethodCandidate(null, diagnostic);

        var target = ProjectMethod(method);
        return new MethodCandidate(target, null);
    }

    /// <summary>
    /// Transform for <see cref="IncrementalGeneratorInitializationContext.SyntaxProvider"/>
    /// <c>.CreateSyntaxProvider</c>: inspects a single class declaration and projects any
    /// Brighter-related interface implementations to value-equatable records.
    /// </summary>
    public static DiscoveryBatch ReadClass(GeneratorSyntaxContext ctx, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var discoverable = GetDiscoverableType(ctx, cancellationToken);
        if (discoverable is null)
            return DiscoveryBatch.Empty;

        var (type, markers) = discoverable.Value;
        var entries = new List<DiscoveredEntry>();
        var diagnostics = new List<DiagnosticInfo>();
        ClassifyEntries(type, markers, entries, diagnostics);

        if (entries.Count == 0 && diagnostics.Count == 0)
            return DiscoveryBatch.Empty;
        return new DiscoveryBatch(
            new EquatableArray<DiscoveredEntry>(entries),
            new EquatableArray<DiagnosticInfo>(diagnostics));
    }

    /// <summary>
    /// Resolve a syntax node to a classifiable type symbol and the framework markers, or null if
    /// the node isn't a discoverable Brighter type. A partial type can carry its base/interface
    /// list on more than one declaration, so the same type may reach this transform multiple times;
    /// we deliberately do NOT dedup on a "primary" declaration (that drops the type when the primary
    /// declaration is the one without a base list). Duplicate entries are collapsed downstream by
    /// RegistrationModel.From's Distinct().
    /// </summary>
    private static (INamedTypeSymbol Type, MarkerSymbols Markers)? GetDiscoverableType(
        GeneratorSyntaxContext ctx, CancellationToken cancellationToken)
    {
        if (ctx.Node is not TypeDeclarationSyntax cls)
            return null;
        if (ctx.SemanticModel.GetDeclaredSymbol(cls, cancellationToken) is not INamedTypeSymbol type)
            return null;
        if (!IsClassifiable(type))
            return null;

        var markers = MarkerSymbols.Resolve(ctx.SemanticModel.Compilation);
        if (!markers.IsValid)
            return null;
        if (markers.ExcludeAttribute is not null && HasAttribute(type, markers.ExcludeAttribute))
            return null;

        return (type, markers);
    }

    private static bool HasAttribute(INamedTypeSymbol type, INamedTypeSymbol attribute) =>
        type.GetAttributes().Any(a => Same(a.AttributeClass, attribute));

    private static DiagnosticInfo? ValidateMethod(IMethodSymbol method, MarkerSymbols markers)
    {
        var location = LocationInfo.From(method.Locations.FirstOrDefault());

        if (!method.IsPartialDefinition)
            return new DiagnosticInfo(Diagnostics.MustBePartial.Id, location, method.Name);

        if (!method.IsStatic)
            return new DiagnosticInfo(Diagnostics.MustBeStatic.Id, location, method.Name);

        if (!Same(method.ReturnType, markers.BrighterBuilder))
            return new DiagnosticInfo(Diagnostics.WrongReturnType.Id, location, method.Name);

        if (method.Parameters.Length != 1 || !Same(method.Parameters[0].Type, markers.BrighterBuilder))
            return new DiagnosticInfo(Diagnostics.WrongSignature.Id, location, method.Name);

        return null;
    }

    private static MethodTarget ProjectMethod(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        var ns = containingType.ContainingNamespace;
        var hasNamespace = ns is { IsGlobalNamespace: false };

        return new MethodTarget(
            Namespace: hasNamespace ? ns!.ToDisplayString() : null,
            ContainingTypeAccessibility: AccessibilityModifier(containingType.DeclaredAccessibility),
            ContainingTypeName: containingType.Name,
            ContainingTypeIsStatic: containingType.IsStatic,
            MethodAccessibility: AccessibilityModifier(method.DeclaredAccessibility),
            MethodName: method.Name,
            ReturnTypeFullyQualified: FullyQualified(method.ReturnType),
            ParameterTypeFullyQualified: FullyQualified(method.Parameters[0].Type),
            ParameterName: method.Parameters[0].Name,
            IsExtensionMethod: method.IsExtensionMethod,
            HintName: BuildHintName(containingType, method.Name));
    }

    private static void ClassifyEntries(
        INamedTypeSymbol type,
        MarkerSymbols markers,
        List<DiscoveredEntry> entries,
        List<DiagnosticInfo> diagnostics)
    {
        // A Brighter type declared inside an open generic can't be named with concrete type
        // arguments at the registration call site, so any registration we emit would reference
        // unbound type parameters and fail to compile. Surface it as BRGEN006 instead — and bail
        // before classifying, because building entries for such a type would itself misfire.
        if (TryReportNestedInOpenGeneric(type, markers, diagnostics))
            return;

        var seenTransform = false;
        var unsupportedGenericMapperOrTransform = false;

        foreach (var iface in type.AllInterfaces)
        {
            var (entry, isTransform, isUnsupported) = TryClassifyInterface(type, iface, markers);
            if (entry is not null)
                entries.Add(entry);
            seenTransform |= isTransform;
            unsupportedGenericMapperOrTransform |= isUnsupported;
        }

        // seenTransform is only set on the non-generic branch, so it already implies !IsGenericType.
        if (seenTransform)
            entries.Add(new DiscoveredEntry(DiscoveredKind.Transform, string.Empty, FullyQualified(type), IsOpenGeneric: false));

        if (unsupportedGenericMapperOrTransform)
        {
            var location = LocationInfo.From(type.Locations.FirstOrDefault());
            diagnostics.Add(new DiagnosticInfo(
                Diagnostics.GenericMapperOrTransformIgnored.Id,
                location,
                FullyQualified(type)));
        }
    }

    private enum BrighterInterfaceKind { None, SyncHandler, AsyncHandler, Mapper, AsyncMapper, Transform }

    // Returns the discovered entry (if any) plus flags the caller folds into its running state, so
    // the per-interface classification stays a pure function of (type, iface) with no ref plumbing.
    private static (DiscoveredEntry? Entry, bool IsTransform, bool IsUnsupportedGeneric) TryClassifyInterface(
        INamedTypeSymbol type,
        INamedTypeSymbol iface,
        MarkerSymbols markers)
    {
        switch (ClassifyInterface(iface, markers, out var requestType))
        {
            case BrighterInterfaceKind.Transform:
                return type.IsGenericType ? (null, false, true) : (null, true, false);
            case BrighterInterfaceKind.SyncHandler:
                return (MakeHandlerEntry(DiscoveredKind.SyncHandler, type, requestType!), false, false);
            case BrighterInterfaceKind.AsyncHandler:
                return (MakeHandlerEntry(DiscoveredKind.AsyncHandler, type, requestType!), false, false);
            case BrighterInterfaceKind.Mapper:
                return MapperClassification(DiscoveredKind.Mapper, type, requestType!);
            case BrighterInterfaceKind.AsyncMapper:
                return MapperClassification(DiscoveredKind.AsyncMapper, type, requestType!);
            default:
                return (null, false, false);
        }
    }

    /// <summary>Classify a single interface the type implements as a Brighter role (or None).</summary>
    private static BrighterInterfaceKind ClassifyInterface(INamedTypeSymbol iface, MarkerSymbols markers, out ITypeSymbol? requestType)
    {
        requestType = null;
        if (Same(iface, markers.MessageTransform) || Same(iface, markers.MessageTransformAsync))
            return BrighterInterfaceKind.Transform;
        if (!iface.IsGenericType || iface.TypeArguments.Length != 1)
            return BrighterInterfaceKind.None;

        requestType = iface.TypeArguments[0];
        return GenericInterfaceKind(iface.OriginalDefinition, markers);
    }

    private static BrighterInterfaceKind GenericInterfaceKind(INamedTypeSymbol def, MarkerSymbols markers)
    {
        if (Same(def, markers.HandleRequests)) return BrighterInterfaceKind.SyncHandler;
        if (Same(def, markers.HandleRequestsAsync)) return BrighterInterfaceKind.AsyncHandler;
        if (Same(def, markers.MessageMapper)) return BrighterInterfaceKind.Mapper;
        if (Same(def, markers.MessageMapperAsync)) return BrighterInterfaceKind.AsyncMapper;
        return BrighterInterfaceKind.None;
    }

    private static DiscoveredEntry MakeHandlerEntry(DiscoveredKind kind, INamedTypeSymbol type, ITypeSymbol requestType)
    {
        if (IsOpenGeneric(type))
            return new DiscoveredEntry(kind, string.Empty, UnboundGenericName(type), IsOpenGeneric: true);
        return new DiscoveredEntry(kind, FullyQualified(requestType), FullyQualified(type), IsOpenGeneric: false);
    }

    private static (DiscoveredEntry? Entry, bool IsTransform, bool IsUnsupportedGeneric) MapperClassification(
        DiscoveredKind kind, INamedTypeSymbol type, ITypeSymbol requestType) =>
        type.IsGenericType
            ? (null, false, true)
            : (new DiscoveredEntry(kind, FullyQualified(requestType), FullyQualified(type), IsOpenGeneric: false), false, false);

    private static bool IsClassifiable(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class)
            return false;
        if (type.IsAbstract || type.IsImplicitClass || type.IsAnonymousType)
            return false;
        return IsReachableFromGeneratedCode(type);
    }

    private static bool IsReachableFromGeneratedCode(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? t = type; t is not null; t = t.ContainingType)
        {
            if (t.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                return false;
        }
        return true;
    }

    // Reports BRGEN006 for a Brighter type nested in an open generic and returns true (telling the
    // caller to stop); returns false for everything else. Kept separate so ClassifyEntries doesn't
    // carry a second block of nested conditionals.
    private static bool TryReportNestedInOpenGeneric(
        INamedTypeSymbol type, MarkerSymbols markers, List<DiagnosticInfo> diagnostics)
    {
        if (!IsNestedInOpenGeneric(type))
            return false;
        if (ImplementsAnyBrighterInterface(type, markers))
            diagnostics.Add(new DiagnosticInfo(
                Diagnostics.NestedInOpenGeneric.Id,
                LocationInfo.From(type.Locations.FirstOrDefault()),
                FullyQualified(type)));
        return true;
    }

    private static bool IsNestedInOpenGeneric(INamedTypeSymbol type)
    {
        for (var outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
        {
            if (outer.IsGenericType)
                return true;
        }
        return false;
    }

    private static bool ImplementsAnyBrighterInterface(INamedTypeSymbol type, MarkerSymbols markers) =>
        type.AllInterfaces.Any(iface => ClassifyInterface(iface, markers, out _) != BrighterInterfaceKind.None);

    private static string BuildHintName(INamedTypeSymbol containingType, string methodName)
    {
        var raw = containingType.ToDisplayString();
        var sanitized = new StringBuilder(raw.Length);
        foreach (var ch in raw)
            sanitized.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        // Append a short stable hash of the original display string so that types differing only
        // in non-identifier characters (e.g. "Foo.Bar" vs "Foo_Bar") don't collide.
        return $"{sanitized}_{Fnv1aHex(raw)}__{methodName}.g.cs";
    }

    private static string Fnv1aHex(string text)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var ch in text)
        {
            hash ^= ch;
            hash *= prime;
        }
        return hash.ToString("x8");
    }

    private static string FullyQualified(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static bool IsOpenGeneric(INamedTypeSymbol type) =>
        type.IsUnboundGenericType || (type.IsGenericType && type.IsDefinition);

    // Only reached for top-level open generics: types nested in an open generic are filtered out
    // earlier (BRGEN006), so the first '<' here always belongs to the type's own parameter list.
    private static string UnboundGenericName(INamedTypeSymbol type)
    {
        var name = FullyQualified(type);
        var lt = name.IndexOf('<');
        if (lt < 0)
            return name;
        var arity = type.TypeParameters.Length;
        return name.Substring(0, lt + 1) + new string(',', arity - 1) + ">";
    }

    private static bool Same(ISymbol? a, ISymbol? b) =>
        SymbolEqualityComparer.Default.Equals(a, b);

    private static string AccessibilityModifier(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.Private => "private",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        _ => "internal"
    };
}
