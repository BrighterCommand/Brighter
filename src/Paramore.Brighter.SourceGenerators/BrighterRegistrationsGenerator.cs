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
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Paramore.Brighter.SourceGenerators.Model;

namespace Paramore.Brighter.SourceGenerators;

/// <summary>
/// Emits a partial implementation of a user-declared method that registers Brighter handlers,
/// message mappers and transforms from the current compilation.
/// </summary>
/// <remarks>
/// The pipeline is structured so that no Roslyn semantic-model object ever escapes a transform —
/// every value flowing through the incremental graph is a value-equatable record. That is what
/// lets the generator skip work when an edit doesn't change the semantically relevant shape of
/// the compilation:
/// <list type="bullet">
///   <item><b>Method stream</b>: <see cref="SemanticModelReader.ReadMethod"/> projects each
///   <c>[BrighterRegistrations]</c>-attributed method to a <see cref="MethodCandidate"/>.</item>
///   <item><b>Discovery stream</b>: <see cref="SemanticModelReader.ReadClass"/> projects each
///   class declaration with a base list to zero or more <see cref="DiscoveredEntry"/> records,
///   collected and flattened into a single equatable array.</item>
///   <item><b>Combine + emit</b>: each method is combined with the discovery snapshot, the
///   resulting <see cref="RegistrationModel"/> is built from pure data, and
///   <see cref="RegistrationWriter"/> turns it into source text.</item>
/// </list>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class BrighterRegistrationsGenerator : IIncrementalGenerator
{
    private const string AttributeName = "Paramore.Brighter.BrighterRegistrationsAttribute";
    private const string AutoRegistrationProperty = "build_property.BrighterAutoRegistration";
    private const string AutoClassNamespace = "Paramore.Brighter.Extensions.DependencyInjection";
    private const string AutoClassName = "BrighterAssemblyRegistrations";
    private const string AutoMethodName = "AddFromThisAssembly";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // [BrighterRegistrations] / [ExcludeFromBrighterRegistration] are declared in core
        // Paramore.Brighter, not emitted here. Emitting them would put an identical internal type
        // in every consuming assembly, which conflicts (CS0436, and CS0121 on the auto-registration
        // extension method) as soon as two of those assemblies see each other through
        // InternalsVisibleTo — the ordinary library/test-project pairing. It would also inject a
        // generated file into every *transitive* consumer of a library that uses the generator.

        var methodCandidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeName,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, ct) => SemanticModelReader.ReadMethod(ctx, ct))
            .WithTrackingName(TrackingNames.MethodCandidates);

        var discoveryBatches = context.SyntaxProvider
            .CreateSyntaxProvider(
                // class or record (mappers/transforms implement interfaces, so they can be records);
                // IsClassifiable still filters to TypeKind.Class, excluding structs/record structs.
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax { BaseList: not null }
                        or RecordDeclarationSyntax { BaseList: not null },
                transform: static (ctx, ct) => SemanticModelReader.ReadClass(ctx, ct))
            .Where(static batch => !batch.IsEmpty)
            .WithTrackingName(TrackingNames.DiscoveryBatches);

        var collectedBatches = discoveryBatches.Collect();

        var discovered = collectedBatches.Select(static (batches, _) =>
            FlattenAndSort(batches.Select(static b => b.Entries)))
            .WithTrackingName(TrackingNames.DiscoveredEntries);

        // Distinct() because a generic type whose base list is split across partial declarations
        // reaches ReadClass once per declaration, producing identical diagnostics (same id + location).
        var discoveryDiagnostics = collectedBatches.Select(static (batches, _) =>
            new EquatableArray<DiagnosticInfo>(
                batches.SelectMany(static b => (IEnumerable<DiagnosticInfo>)b.Diagnostics).Distinct()))
            .WithTrackingName(TrackingNames.DiscoveryDiagnostics);

        var collectedCandidates = methodCandidates.Collect();

        // True when the compilation hand-writes at least one valid [BrighterRegistrations] method.
        // Used to suppress the auto class so the two paths can't both register (double registration,
        // or an ambiguous AddFromThisAssembly call when the manual method shares the name).
        var hasManualRegistration = collectedCandidates
            .Select(static (candidates, _) => candidates.Any(static c => c.Method is not null));

        // True when the compilation *attempted* a manual registration, valid or not. A rejected
        // attempt still means the user has been told what is wrong, with a location — so the auto
        // path stays quiet rather than reporting the same missing reference a second time.
        var attemptedManualRegistration = collectedCandidates
            .Select(static (candidates, _) => candidates.Length > 0);

        var autoSetting = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => ReadAutoSetting(provider.GlobalOptions));

        // The generator only emits registrations when auto-registration is on or a manual method is
        // present. Gate discovery diagnostics (BRGEN005/006) on that, so a transitive analyzer
        // reference that generates nothing doesn't warn about the consumer's generic /
        // nested-in-open-generic types.
        var generatorActive = autoSetting.Combine(hasManualRegistration)
            .Select(static (pair, _) => pair.Left.Enabled || pair.Right);

        context.RegisterSourceOutput(discoveryDiagnostics.Combine(generatorActive), static (spc, pair) =>
        {
            var (diagnostics, active) = pair;
            if (!active)
                return;
            foreach (var info in diagnostics)
                spc.ReportDiagnostic(ToDiagnostic(info));
        });

        var combined = methodCandidates.Combine(discovered)
            .WithTrackingName(TrackingNames.RegistrationInputs);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var (candidate, entries) = pair;

            if (candidate.Diagnostic is not null)
            {
                spc.ReportDiagnostic(ToDiagnostic(candidate.Diagnostic));
                return;
            }

            if (candidate.Method is null)
                return;

            // Always emitted, even with nothing to register: the user declared a partial method, and
            // leaving it unimplemented is a compile error (CS8795). The auto form has no such
            // obligation and is suppressed when empty — see RegisterAutoRegistration.
            var model = RegistrationModel.From(candidate.Method, entries);
            ReportDuplicateHandlers(spc, model);
            spc.AddSource(model.Target.HintName, SourceText.From(RegistrationWriter.Write(model), Encoding.UTF8));
        });

        RegisterAutoRegistration(context, discovered, autoSetting, hasManualRegistration, attemptedManualRegistration);
    }

    /// <summary>
    /// The <c>BrighterAutoRegistration</c> MSBuild property as the generator sees it. A value that
    /// is present but not a boolean is carried through rather than silently treated as "off", so
    /// the user hears about the typo (BRGEN013) instead of losing all their registrations to it.
    /// </summary>
    private sealed record AutoSetting(bool Enabled, string? InvalidValue)
    {
        public static readonly AutoSetting Absent = new(false, null);
    }

    private static AutoSetting ReadAutoSetting(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(AutoRegistrationProperty, out var value) || string.IsNullOrWhiteSpace(value))
            return AutoSetting.Absent;
        if (!bool.TryParse(value, out var enabled))
            return new AutoSetting(false, value);
        return new AutoSetting(enabled, null);
    }

    /// <summary>
    /// Second pipeline that synthesises an <c>internal static class BrighterAssemblyRegistrations</c>
    /// with an <c>AddFromThisAssembly</c> extension method, when the <c>BrighterAutoRegistration</c>
    /// MSBuild property is true (set by the <c>build/</c> props file shipped with the package, so it
    /// only flows to direct PackageReferences — not transitively).
    /// </summary>
    private static void RegisterAutoRegistration(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<EquatableArray<DiscoveredEntry>> discovered,
        IncrementalValueProvider<AutoSetting> autoSetting,
        IncrementalValueProvider<bool> hasManualRegistration,
        IncrementalValueProvider<bool> attemptedManualRegistration)
    {
        var facts = context.CompilationProvider
            .Select(static (c, _) => CompilationFacts.For(c));

        var manualSignals = hasManualRegistration.Combine(attemptedManualRegistration);
        var autoInputs = autoSetting.Combine(facts).Combine(discovered).Combine(manualSignals);

        context.RegisterSourceOutput(autoInputs, static (spc, pair) =>
        {
            var (((setting, compilationFacts), entries), (hasManual, attemptedManual)) = pair;

            if (setting.InvalidValue is not null)
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.InvalidAutoRegistrationValue, Location.None, setting.InvalidValue));

            if (!setting.Enabled)
                return;

            // Auto-registration was asked for, but discovery needs both core Brighter and the DI
            // package to resolve its marker interfaces. Say so: the layered case (a domain library
            // referencing core only) otherwise builds clean and registers nothing. Unless the user
            // also wrote a [BrighterRegistrations] method — BRGEN009 has already said it there, on
            // the declaration, and one root cause deserves one diagnostic.
            if (!compilationFacts.BrighterAvailable)
            {
                if (!attemptedManual)
                    spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.AutoRegistrationBrighterNotReferenced, Location.None));
                return;
            }

            // A hand-written [BrighterRegistrations] method takes precedence; tell the user why the
            // auto class disappeared rather than leaving them to discover it.
            if (hasManual)
            {
                spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.AutoRegistrationSuppressed, Location.None));
                return;
            }

            // The name is spoken for in this compilation's own source; emitting would be CS0101 in
            // generated code the user cannot edit.
            if (compilationFacts.AutoClassNameTaken)
            {
                spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.AutoRegistrationNameTaken, Location.None));
                return;
            }

            var model = RegistrationModel.From(BuildAutoTarget(), entries);

            // Nothing to register: emitting anyway would put an AddFromThisAssembly() on the builder
            // that compiles, reads as if it registered the solution's handlers, and registers
            // nothing — the exact silent-miss this generator exists to prevent. Leaving it out turns
            // that into a compile error at the call site instead.
            if (!model.HasRegistrations)
                return;

            if (compilationFacts.CollidingAssembly is not null)
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.AutoRegistrationCollision, Location.None, compilationFacts.CollidingAssembly));

            ReportDuplicateHandlers(spc, model);
            spc.AddSource(model.Target.HintName, SourceText.From(RegistrationWriter.Write(model), Encoding.UTF8));
        });
    }

    /// <summary>
    /// Compilation-derived inputs for the auto path, bundled into one value so the (cheap, but
    /// per-edit) compilation lookups happen once and downstream stages compare by value.
    /// </summary>
    /// <param name="CollidingAssembly">
    /// The name of a <em>referenced</em> assembly that already exposes
    /// <c>BrighterAssemblyRegistrations</c> to this compilation, or null. Only an
    /// <c>InternalsVisibleTo</c> grant can make another assembly's copy visible — the ordinary
    /// library/test-project pairing — and when it does, both copies offer the same
    /// <c>AddFromThisAssembly</c> extension and every call site becomes ambiguous (CS0121).
    /// </param>
    /// <param name="AutoClassNameTaken">
    /// True when <em>this</em> compilation's own source already declares that type, which would make
    /// the generated file a duplicate definition (CS0101).
    /// </param>
    private sealed record CompilationFacts(bool BrighterAvailable, string? CollidingAssembly, bool AutoClassNameTaken)
    {
        // Nothing generated by RegisterSourceOutput is in the compilation the generator sees, so
        // every hit below is genuinely somebody's hand-written (or previously compiled) type.
        public static CompilationFacts For(Compilation compilation)
        {
            string? collidingAssembly = null;
            var nameTaken = false;

            foreach (var type in compilation.GetTypesByMetadataName($"{AutoClassNamespace}.{AutoClassName}"))
            {
                if (SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly))
                    nameTaken = true;
                else if (collidingAssembly is null && compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
                    collidingAssembly = type.ContainingAssembly.Name;
            }

            return new CompilationFacts(MarkerSymbols.Resolve(compilation).IsValid, collidingAssembly, nameTaken);
        }
    }

    /// <summary>
    /// Reports BRGEN012 for every non-event request this model registers two or more handlers for.
    /// The scanner has always allowed this and failed at dispatch; the generator can see it while
    /// the build is still running.
    /// </summary>
    private static void ReportDuplicateHandlers(SourceProductionContext spc, RegistrationModel model)
    {
        foreach (var (requestType, handlerTypes) in model.DuplicateHandlers())
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.DuplicateHandler,
                Location.None,
                requestType,
                handlerTypes.Count,
                string.Join(", ", handlerTypes)));
        }
    }

    private static MethodTarget BuildAutoTarget() => new(
        Namespace: AutoClassNamespace,
        ContainingTypeAccessibility: "internal",
        ContainingTypeName: AutoClassName,
        ContainingTypeIsStatic: true,
        MethodAccessibility: "internal",
        MethodName: AutoMethodName,
        ReturnTypeFullyQualified: "global::Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder",
        ParameterTypeFullyQualified: "global::Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder",
        ParameterName: "builder",
        IsExtensionMethod: true,
        HintName: $"{AutoClassName}__{AutoMethodName}.g.cs",
        IsPartial: false);

    /// <summary>
    /// Concatenate the per-file discovery batches into one equatable array, sorted for stable
    /// emit order across runs.
    /// </summary>
    private static EquatableArray<DiscoveredEntry> FlattenAndSort(IEnumerable<EquatableArray<DiscoveredEntry>> batches)
    {
        IEnumerable<DiscoveredEntry> flat = batches.SelectMany(static b => (IEnumerable<DiscoveredEntry>)b);
        var ordered = flat
            .OrderBy(static e => e.Kind)
            .ThenBy(static e => e.TypeFullyQualified, System.StringComparer.Ordinal)
            .ThenBy(static e => e.RequestTypeFullyQualified, System.StringComparer.Ordinal);
        return new EquatableArray<DiscoveredEntry>(ordered);
    }

    private static Diagnostic ToDiagnostic(DiagnosticInfo info) => Diagnostic.Create(
        DescriptorFor(info.Id),
        info.Location?.ToLocation() ?? Location.None,
        info.Argument);

    private static readonly Dictionary<string, DiagnosticDescriptor> s_descriptorsById = new()
    {
        [Diagnostics.MustBePartial.Id] = Diagnostics.MustBePartial,
        [Diagnostics.MustBeStatic.Id] = Diagnostics.MustBeStatic,
        [Diagnostics.WrongReturnType.Id] = Diagnostics.WrongReturnType,
        [Diagnostics.WrongSignature.Id] = Diagnostics.WrongSignature,
        [Diagnostics.GenericMapperOrTransformIgnored.Id] = Diagnostics.GenericMapperOrTransformIgnored,
        [Diagnostics.NestedInOpenGeneric.Id] = Diagnostics.NestedInOpenGeneric,
        [Diagnostics.UnsupportedContainingType.Id] = Diagnostics.UnsupportedContainingType,
        [Diagnostics.BrighterNotReferenced.Id] = Diagnostics.BrighterNotReferenced,
        [Diagnostics.AutoRegistrationBrighterNotReferenced.Id] = Diagnostics.AutoRegistrationBrighterNotReferenced,
        [Diagnostics.AutoRegistrationCollision.Id] = Diagnostics.AutoRegistrationCollision,
        [Diagnostics.DuplicateHandler.Id] = Diagnostics.DuplicateHandler,
        [Diagnostics.AutoRegistrationNameTaken.Id] = Diagnostics.AutoRegistrationNameTaken,
        [Diagnostics.InvalidAutoRegistrationValue.Id] = Diagnostics.InvalidAutoRegistrationValue,
    };

    private static DiagnosticDescriptor DescriptorFor(string id) =>
        s_descriptorsById.TryGetValue(id, out var descriptor)
            ? descriptor
            : throw new System.InvalidOperationException($"Unknown Brighter diagnostic id '{id}' — DescriptorFor needs updating.");
}
