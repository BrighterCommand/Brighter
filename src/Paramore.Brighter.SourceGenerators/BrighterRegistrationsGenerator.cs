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

    private static readonly string AttributeSource = GeneratedSource.Header + "\n" + AttributeBody;

    private const string AttributeBody = """
        #nullable enable
        namespace Paramore.Brighter
        {
            /// <summary>
            /// Marks a <c>partial</c> method that the Brighter source generator will implement
            /// to register handlers and message mappers discovered in the current compilation.
            /// The method must be <c>static partial</c>, return <see cref="Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder"/>,
            /// and take a single <see cref="Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder"/> parameter (extension methods supported).
            /// </summary>
            [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
            internal sealed class BrighterRegistrationsAttribute : global::System.Attribute
            {
            }

            /// <summary>
            /// Excludes a handler / mapper / transform type from automatic Brighter registration.
            /// </summary>
            [global::System.AttributeUsage(global::System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
            internal sealed class ExcludeFromBrighterRegistrationAttribute : global::System.Attribute
            {
            }
        }
        """;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource("BrighterRegistrationsAttributes.g.cs", SourceText.From(AttributeSource, Encoding.UTF8)));

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
                batches.SelectMany(static b => (IEnumerable<DiagnosticInfo>)b.Diagnostics).Distinct()));

        context.RegisterSourceOutput(discoveryDiagnostics, static (spc, diagnostics) =>
        {
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

            var model = RegistrationModel.From(candidate.Method, entries);
            spc.AddSource(model.HintName, SourceText.From(RegistrationWriter.Write(model), Encoding.UTF8));
        });

        // True when the compilation hand-writes at least one valid [BrighterRegistrations] method.
        // Used to suppress the auto class so the two paths can't both register (double registration,
        // or an ambiguous AddFromThisAssembly call when the manual method shares the name).
        var hasManualRegistration = methodCandidates
            .Collect()
            .Select(static (candidates, _) => candidates.Any(static c => c.Method is not null));

        RegisterAutoRegistration(context, discovered, hasManualRegistration);
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
        IncrementalValueProvider<bool> hasManualRegistration)
    {
        var autoEnabled = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
                provider.GlobalOptions.TryGetValue(AutoRegistrationProperty, out var v)
                && bool.TryParse(v, out var b)
                && b);

        var brighterAvailable = context.CompilationProvider
            .Select(static (c, _) => MarkerSymbols.Resolve(c).IsValid);

        var autoInputs = autoEnabled.Combine(brighterAvailable).Combine(discovered).Combine(hasManualRegistration);

        context.RegisterSourceOutput(autoInputs, static (spc, pair) =>
        {
            var (((enabled, available), entries), hasManual) = pair;
            if (!enabled || !available)
                return;

            // A hand-written [BrighterRegistrations] method takes precedence; tell the user why the
            // auto class disappeared rather than leaving them to discover it.
            if (hasManual)
            {
                spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.AutoRegistrationSuppressed, Location.None));
                return;
            }

            var target = BuildAutoTarget();
            var model = RegistrationModel.From(target, entries);
            spc.AddSource(model.HintName, SourceText.From(RegistrationWriter.Write(model), Encoding.UTF8));
        });
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

    private static DiagnosticDescriptor DescriptorFor(string id) => id switch
    {
        "BRGEN001" => Diagnostics.MustBePartial,
        "BRGEN002" => Diagnostics.MustBeStatic,
        "BRGEN003" => Diagnostics.WrongReturnType,
        "BRGEN004" => Diagnostics.WrongSignature,
        "BRGEN005" => Diagnostics.GenericMapperOrTransformIgnored,
        "BRGEN006" => Diagnostics.NestedInOpenGeneric,
        _ => throw new System.InvalidOperationException($"Unknown Brighter diagnostic id '{id}' — DescriptorFor needs updating."),
    };
}
