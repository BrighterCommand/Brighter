using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.SourceGenerators.Tests;

/// <summary>
/// End-to-end "validation" tests: drive the generator through Microsoft.CodeAnalysis.Testing,
/// supply user source, and assert on the generated source against an expected document.
/// </summary>
public class BrighterRegistrationsGeneratorTests
{
    private static CSharpSourceGeneratorTest<BrighterRegistrationsGenerator, DefaultVerifier> MakeTest() =>
        new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestState =
            {
                AdditionalReferences =
                {
                    MetadataReference.CreateFromFile(typeof(IRequest).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(IBrighterBuilder).Assembly.Location),
                },
            },
        };

    [Fact]
    public async Task NoBrighterReference_GeneratesAttributesOnly()
    {
        var test = new CSharpSourceGeneratorTest<BrighterRegistrationsGenerator, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestState =
            {
                Sources = { "// no user code" },
            },
        };
        // Post-init output is still emitted; only the per-method registration is skipped.
        test.TestState.GeneratedSources.Add(AttributeFile());

        await test.RunAsync();
    }

    [Fact]
    public async Task SyncHandler_GeneratesRegistration()
    {
        const string userCode = """
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public class GreetingCommand : Command
            {
                public GreetingCommand() : base(System.Guid.NewGuid()) { }
            }

            public class GreetingHandler : RequestHandler<GreetingCommand>
            {
                public override GreetingCommand Handle(GreetingCommand command) => base.Handle(command);
            }

            public static partial class Registrations
            {
                [BrighterRegistrations]
                public static partial IBrighterBuilder AddFromThisAssembly(this IBrighterBuilder builder);
            }
            """;

        var test = MakeTest();
        test.TestState.Sources.Add(userCode);
        test.TestState.GeneratedSources.Add(AttributeFile());
        test.TestState.GeneratedSources.Add(Registration("""
            builder.Handlers(r =>
            {
                r.Register<global::App.GreetingCommand, global::App.GreetingHandler>();
            });
            """));

        await test.RunAsync();
    }

    [Fact]
    public async Task ExcludedType_IsNotRegistered()
    {
        const string userCode = """
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public class GreetingCommand : Command
            {
                public GreetingCommand() : base(System.Guid.NewGuid()) { }
            }

            [ExcludeFromBrighterRegistration]
            public class GreetingHandler : RequestHandler<GreetingCommand>
            {
                public override GreetingCommand Handle(GreetingCommand command) => base.Handle(command);
            }

            public static partial class Registrations
            {
                [BrighterRegistrations]
                public static partial IBrighterBuilder AddFromThisAssembly(this IBrighterBuilder builder);
            }
            """;

        var test = MakeTest();
        test.TestState.Sources.Add(userCode);
        test.TestState.GeneratedSources.Add(AttributeFile());
        test.TestState.GeneratedSources.Add(Registration(""));

        await test.RunAsync();
    }

    [Fact]
    public async Task AsyncHandler_GeneratesRegisterAsync()
    {
        const string userCode = """
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public class GreetingCommand : Command
            {
                public GreetingCommand() : base(System.Guid.NewGuid()) { }
            }

            public class GreetingHandler : RequestHandlerAsync<GreetingCommand>
            {
            }

            public static partial class Registrations
            {
                [BrighterRegistrations]
                public static partial IBrighterBuilder AddFromThisAssembly(this IBrighterBuilder builder);
            }
            """;

        var test = MakeTest();
        test.TestState.Sources.Add(userCode);
        test.TestState.GeneratedSources.Add(AttributeFile());
        test.TestState.GeneratedSources.Add(Registration("""
            builder.AsyncHandlers(r =>
            {
                r.RegisterAsync<global::App.GreetingCommand, global::App.GreetingHandler>();
            });
            """));

        await test.RunAsync();
    }

    [Fact]
    public async Task MapperAndAsyncMapperAndTransform_AreAllDiscovered()
    {
        const string userCode = """
            using System;
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public class GreetingEvent : Event { public GreetingEvent() : base(System.Guid.NewGuid()) { } }

            public class GreetingMapper : IAmAMessageMapper<GreetingEvent>
            {
                public IRequestContext? Context { get; set; }
                public Message MapToMessage(GreetingEvent request, Publication publication) => new();
                public GreetingEvent MapToRequest(Message message) => new();
            }

            public class GreetingMapperAsync : IAmAMessageMapperAsync<GreetingEvent>
            {
                public IRequestContext? Context { get; set; }
                public System.Threading.Tasks.Task<Message> MapToMessageAsync(GreetingEvent request, Publication publication, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.FromResult(new Message());
                public System.Threading.Tasks.Task<GreetingEvent> MapToRequestAsync(Message message, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.FromResult(new GreetingEvent());
            }

            public class NoOp : IAmAMessageTransform
            {
                public IRequestContext? Context { get; set; }
                public void InitializeWrapFromAttributeParams(params object?[] init) { }
                public void InitializeUnwrapFromAttributeParams(params object?[] init) { }
                public Message Wrap(Message m, Publication p) => m;
                public Message Unwrap(Message m) => m;
                public void Dispose() { }
            }

            public static partial class Registrations
            {
                [BrighterRegistrations]
                public static partial IBrighterBuilder AddFromThisAssembly(this IBrighterBuilder builder);
            }
            """;

        var test = MakeTest();
        test.TestState.Sources.Add(userCode);
        test.TestState.GeneratedSources.Add(AttributeFile());
        test.TestState.GeneratedSources.Add(Registration("""
            builder.MapperRegistry(r =>
            {
                r.Add(typeof(global::App.GreetingEvent), typeof(global::App.GreetingMapper));
                r.AddAsync(typeof(global::App.GreetingEvent), typeof(global::App.GreetingMapperAsync));
            });
            global::Paramore.Brighter.Extensions.DependencyInjection.BrighterBuilderExtensions.Transforms(builder, r =>
            {
                r.Add(typeof(global::App.NoOp));
            });
            """));

        await test.RunAsync();
    }

    [Fact]
    public async Task PartialHandler_IsRegisteredOnce()
    {
        const string partA = """
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public class GreetingCommand : Command
            {
                public GreetingCommand() : base(System.Guid.NewGuid()) { }
            }

            public partial class SplitHandler : RequestHandler<GreetingCommand>
            {
                public override GreetingCommand Handle(GreetingCommand command) => base.Handle(command);
            }

            public static partial class Registrations
            {
                [BrighterRegistrations]
                public static partial IBrighterBuilder AddFromThisAssembly(this IBrighterBuilder builder);
            }
            """;

        const string partB = """
            namespace App;

            public partial class SplitHandler
            {
                public void Helper() { }
            }
            """;

        var test = MakeTest();
        test.TestState.Sources.Add(partA);
        test.TestState.Sources.Add(partB);
        test.TestState.GeneratedSources.Add(AttributeFile());
        test.TestState.GeneratedSources.Add(Registration("""
            builder.Handlers(r =>
            {
                r.Register<global::App.GreetingCommand, global::App.SplitHandler>();
            });
            """));

        await test.RunAsync();
    }

    [Fact]
    public async Task AbstractAndPrivateNestedHandlers_AreFilteredOut()
    {
        const string userCode = """
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public class GreetingCommand : Command
            {
                public GreetingCommand() : base(System.Guid.NewGuid()) { }
            }

            public abstract class AbstractHandler : RequestHandler<GreetingCommand>
            {
            }

            internal class Outer
            {
                private class PrivateNested : RequestHandler<GreetingCommand>
                {
                    public override GreetingCommand Handle(GreetingCommand command) => base.Handle(command);
                }
            }

            public static partial class Registrations
            {
                [BrighterRegistrations]
                public static partial IBrighterBuilder AddFromThisAssembly(this IBrighterBuilder builder);
            }
            """;

        var test = MakeTest();
        test.TestState.Sources.Add(userCode);
        test.TestState.GeneratedSources.Add(AttributeFile());
        test.TestState.GeneratedSources.Add(Registration(""));

        await test.RunAsync();
    }

    [Fact]
    public async Task GenericMapper_ReportsBRGEN005()
    {
        const string userCode = """
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public class GreetingEvent : Event { public GreetingEvent() : base(System.Guid.NewGuid()) { } }

            public class OpenMapper<T> : IAmAMessageMapper<GreetingEvent>
            {
                public IRequestContext? Context { get; set; }
                public Message MapToMessage(GreetingEvent request, Publication publication) => new();
                public GreetingEvent MapToRequest(Message message) => new();
            }

            public static partial class Registrations
            {
                [BrighterRegistrations]
                public static partial IBrighterBuilder AddFromThisAssembly(this IBrighterBuilder builder);
            }
            """;

        var test = MakeTest();
        test.TestState.Sources.Add(userCode);
        test.TestState.GeneratedSources.Add(AttributeFile());
        test.TestState.GeneratedSources.Add(Registration(""));
        test.TestState.ExpectedDiagnostics.Add(
            DiagnosticResult.CompilerWarning("BRGEN005").WithSpan(8, 14, 8, 24).WithArguments("global::App.OpenMapper<T>"));

        await test.RunAsync();
    }

    [Fact]
    public async Task NonStaticMethod_ReportsBRGEN002()
    {
        const string userCode = """
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public partial class Registrations
            {
                [BrighterRegistrations]
                public partial IBrighterBuilder AddFromThisAssembly(IBrighterBuilder builder);
            }
            """;

        var test = MakeTest();
        test.TestState.Sources.Add(userCode);
        test.TestState.GeneratedSources.Add(AttributeFile());
        test.TestState.ExpectedDiagnostics.Add(
            DiagnosticResult.CompilerError("BRGEN002").WithSpan(9, 37, 9, 56).WithArguments("AddFromThisAssembly"));
        // The C# compiler will also complain that the partial method has no implementation;
        // we don't care about its exact diagnostics here.
        test.CompilerDiagnostics = CompilerDiagnostics.None;

        await test.RunAsync();
    }

    [Fact]
    public async Task WrongReturnType_ReportsBRGEN003()
    {
        const string userCode = """
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public static partial class Registrations
            {
                [BrighterRegistrations]
                public static partial void AddFromThisAssembly(IBrighterBuilder builder);
            }
            """;

        var test = MakeTest();
        test.TestState.Sources.Add(userCode);
        test.TestState.GeneratedSources.Add(AttributeFile());
        test.TestState.ExpectedDiagnostics.Add(
            DiagnosticResult.CompilerError("BRGEN003").WithSpan(9, 32, 9, 51).WithArguments("AddFromThisAssembly"));
        test.CompilerDiagnostics = CompilerDiagnostics.None;

        await test.RunAsync();
    }

    [Fact]
    public async Task WrongSignature_ReportsBRGEN004()
    {
        const string userCode = """
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public static partial class Registrations
            {
                [BrighterRegistrations]
                public static partial IBrighterBuilder AddFromThisAssembly(IBrighterBuilder builder, int extra);
            }
            """;

        var test = MakeTest();
        test.TestState.Sources.Add(userCode);
        test.TestState.GeneratedSources.Add(AttributeFile());
        test.TestState.ExpectedDiagnostics.Add(
            DiagnosticResult.CompilerError("BRGEN004").WithSpan(9, 44, 9, 63).WithArguments("AddFromThisAssembly"));
        test.CompilerDiagnostics = CompilerDiagnostics.None;

        await test.RunAsync();
    }

    [Fact]
    public async Task NonPartialMethod_ReportsBRGEN001()
    {
        const string userCode = """
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public static class Registrations
            {
                [BrighterRegistrations]
                public static IBrighterBuilder AddFromThisAssembly(IBrighterBuilder builder) => builder;
            }
            """;

        var test = MakeTest();
        test.TestState.Sources.Add(userCode);
        test.TestState.GeneratedSources.Add(AttributeFile());
        test.TestState.ExpectedDiagnostics.Add(
            DiagnosticResult.CompilerError("BRGEN001").WithSpan(9, 36, 9, 55).WithArguments("AddFromThisAssembly"));

        await test.RunAsync();
    }

    [Fact]
    public async Task RecordMapper_IsDiscovered()
    {
        // Mappers/transforms implement interfaces only, so they may legitimately be records.
        const string userCode = """
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public class GreetingEvent : Event { public GreetingEvent() : base(System.Guid.NewGuid()) { } }

            public record GreetingMapper : IAmAMessageMapper<GreetingEvent>
            {
                public IRequestContext? Context { get; set; }
                public Message MapToMessage(GreetingEvent request, Publication publication) => new();
                public GreetingEvent MapToRequest(Message message) => new();
            }

            public static partial class Registrations
            {
                [BrighterRegistrations]
                public static partial IBrighterBuilder AddFromThisAssembly(this IBrighterBuilder builder);
            }
            """;

        var test = MakeTest();
        test.TestState.Sources.Add(userCode);
        test.TestState.GeneratedSources.Add(AttributeFile());
        test.TestState.GeneratedSources.Add(Registration("""
            builder.MapperRegistry(r =>
            {
                r.Add(typeof(global::App.GreetingEvent), typeof(global::App.GreetingMapper));
            });
            """));

        await test.RunAsync();
    }

    [Fact]
    public async Task PartialHandler_WithBaseListOnSecondaryFile_IsRegistered()
    {
        // The declaration WITHOUT the base list sorts first ("primary"); the base list lives on the
        // second file. Regression test for the dropped-registration bug — the handler must still be
        // discovered and registered.
        const string partA = """
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public class GreetingCommand : Command
            {
                public GreetingCommand() : base(System.Guid.NewGuid()) { }
            }

            public partial class SplitHandler
            {
                public void Helper() { }
            }

            public static partial class Registrations
            {
                [BrighterRegistrations]
                public static partial IBrighterBuilder AddFromThisAssembly(this IBrighterBuilder builder);
            }
            """;

        const string partB = """
            using Paramore.Brighter;

            namespace App;

            public partial class SplitHandler : RequestHandler<GreetingCommand>
            {
                public override GreetingCommand Handle(GreetingCommand command) => base.Handle(command);
            }
            """;

        var test = MakeTest();
        test.TestState.Sources.Add(partA);
        test.TestState.Sources.Add(partB);
        test.TestState.GeneratedSources.Add(AttributeFile());
        test.TestState.GeneratedSources.Add(Registration("""
            builder.Handlers(r =>
            {
                r.Register<global::App.GreetingCommand, global::App.SplitHandler>();
            });
            """));

        await test.RunAsync();
    }

    [Fact]
    public void HandlerNestedInOpenGeneric_ReportsBRGEN006_AndEmitsNoBrokenRegistration()
    {
        // A handler nested in an open generic can't be named with concrete type args at the call
        // site; the generator must surface BRGEN006 rather than emit code referencing unbound T.
        var result = RunDriver("""
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public class GreetingCommand : Command
            {
                public GreetingCommand() : base(System.Guid.NewGuid()) { }
            }

            public class Outer<T>
            {
                public class Handler : RequestHandler<GreetingCommand>
                {
                    public override GreetingCommand Handle(GreetingCommand command) => base.Handle(command);
                }
            }

            public static partial class Registrations
            {
                [BrighterRegistrations]
                public static partial IBrighterBuilder AddFromThisAssembly(this IBrighterBuilder builder);
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "BRGEN006");

        var generated = GeneratedText(result);
        Assert.DoesNotContain(".Handlers(", generated);
        Assert.DoesNotContain("Outer<T>", generated);
    }

    [Fact]
    public void GenericMapperSplitAcrossPartials_ReportsSingleBRGEN005()
    {
        // Each base-list-bearing partial declaration reaches ReadClass and emits BRGEN005; the
        // flattened diagnostics must be de-duplicated so the user sees it once, not once per file.
        var result = RunDriver(
            """
            using Paramore.Brighter;
            using Paramore.Brighter.Extensions.DependencyInjection;

            namespace App;

            public class GreetingEvent : Event { public GreetingEvent() : base(System.Guid.NewGuid()) { } }

            public partial class OpenMapper<T> : IAmAMessageMapper<GreetingEvent>
            {
                public IRequestContext? Context { get; set; }
                public Message MapToMessage(GreetingEvent request, Publication publication) => new();
                public GreetingEvent MapToRequest(Message message) => new();
            }
            """,
            """
            namespace App;

            public partial class OpenMapper<T> : System.IDisposable
            {
                public void Dispose() { }
            }
            """);

        Assert.Equal(1, result.Diagnostics.Count(d => d.Id == "BRGEN005"));
    }

    private static GeneratorRunResult RunDriver(params string[] sources)
    {
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Paramore.Brighter.IRequest).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder).Assembly.Location),
        };
        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();
        var compilation = CSharpCompilation.Create(
            "TestAsm", trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new BrighterRegistrationsGenerator().AsSourceGenerator() });
        return driver.RunGenerators(compilation).GetRunResult().Results.Single();
    }

    private static string GeneratedText(GeneratorRunResult result) =>
        string.Join("\n", result.GeneratedSources.Select(g => g.SourceText.ToString()));

    private const string RegistrationHint = "App_Registrations_6a7651d4__AddFromThisAssembly.g.cs";

    /// <summary>
    /// Build the expected generated registration file from its variable body. The banner and the
    /// <c>[GeneratedCode]</c> attribute are sourced from <see cref="GeneratedSource"/> so the tests
    /// track the production text (and the tool version) automatically. <paramref name="body"/> uses
    /// indentation relative to the method body (top-level statements at column 0); each line is
    /// shifted to the method's three-level indent.
    /// </summary>
    private static (System.Type, string, string) Registration(string body) =>
        (typeof(BrighterRegistrationsGenerator), RegistrationHint, ExpectedRegistration(body));

    private static string ExpectedRegistration(string body)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(GeneratedSource.Header).Append('\n');
        sb.Append("#nullable enable\n\n");
        sb.Append("namespace App\n{\n");
        sb.Append("    public static partial class Registrations\n    {\n");
        sb.Append("        ").Append(GeneratedSource.GeneratedCodeAttribute).Append('\n');
        sb.Append("        public static partial global::Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder AddFromThisAssembly(this global::Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder builder)\n");
        sb.Append("        {\n");
        if (!string.IsNullOrEmpty(body))
        {
            foreach (var line in body.Replace("\r\n", "\n").Split('\n'))
                sb.Append(line.Length == 0 ? string.Empty : new string(' ', 12) + line).Append('\n');
        }
        sb.Append("            return builder;\n");
        sb.Append("        }\n    }\n}\n");
        return sb.ToString();
    }

    private static (System.Type, string, string) AttributeFile() => (
        typeof(BrighterRegistrationsGenerator),
        "BrighterRegistrationsAttributes.g.cs",
        GeneratedSource.Header + "\n" + """
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
        """);
}
