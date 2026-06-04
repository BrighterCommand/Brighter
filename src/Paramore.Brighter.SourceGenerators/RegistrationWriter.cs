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

using System.IO;
using System.Text;
using Paramore.Brighter.SourceGenerators.Model;

namespace Paramore.Brighter.SourceGenerators;

/// <summary>
/// Pure function that turns a <see cref="RegistrationModel"/> into the C# source for the
/// partial method implementation. Holds no Roslyn references, so it can be unit-tested
/// without constructing a Compilation. Indentation is managed structurally by
/// <see cref="CodeWriter"/> rather than by embedding whitespace in the emitted strings.
/// </summary>
public static class RegistrationWriter
{
    public static string Write(RegistrationModel model)
    {
        var buffer = new StringWriter();
        using var code = new CodeWriter(buffer);

        code.WriteLine(GeneratedSource.Header);
        code.WriteLine("#nullable enable");
        code.WriteLineNoTabs(string.Empty);

        var hasNamespace = !string.IsNullOrEmpty(model.Namespace);
        if (hasNamespace)
        {
            code.WriteLine($"namespace {model.Namespace}");
            code.StartBlock();
        }
        else
        {
            // Preserve the historical layout: the registration class is indented one level even
            // when there is no namespace wrapper to open a block.
            code.Indent++;
        }

        WriteContainingType(code, model);

        if (hasNamespace)
            code.EndBlock();

        code.Flush();
        return buffer.ToString();
    }

    private static void WriteContainingType(CodeWriter code, RegistrationModel model)
    {
        code.WriteLine(ContainingTypeDeclaration(model));
        code.StartBlock();

        code.WriteLine(GeneratedSource.GeneratedCodeAttribute);
        code.WriteLine(MethodSignature(model));
        code.StartBlock();

        WriteHandlers(code, model.ParameterName, model.Handlers, isAsync: false);
        WriteHandlers(code, model.ParameterName, model.AsyncHandlers, isAsync: true);
        WriteMappers(code, model.ParameterName, model.Mappers, model.AsyncMappers);
        WriteTransforms(code, model.ParameterName, model.Transforms);

        code.WriteLine($"return {model.ParameterName};");

        code.EndBlock(); // method
        code.EndBlock(); // containing type
    }

    private static string ContainingTypeDeclaration(RegistrationModel model)
    {
        var typeKeyword = (model.ContainingTypeIsStatic, model.IsPartial) switch
        {
            (true, true) => "static partial class",
            (true, false) => "static class",
            (false, true) => "partial class",
            (false, false) => "class",
        };
        return $"{model.ContainingTypeAccessibility} {typeKeyword} {model.ContainingTypeName}";
    }

    private static string MethodSignature(RegistrationModel model)
    {
        var sb = new StringBuilder();
        sb.Append(model.MethodAccessibility).Append(" static ");
        if (model.IsPartial)
            sb.Append("partial ");
        sb.Append(model.ReturnTypeFullyQualified).Append(' ').Append(model.MethodName).Append('(');
        if (model.IsExtensionMethod)
            sb.Append("this ");
        sb.Append(model.ParameterTypeFullyQualified).Append(' ').Append(model.ParameterName).Append(')');
        return sb.ToString();
    }

    private static void WriteHandlers(
        CodeWriter code,
        string paramName,
        EquatableArray<HandlerEntry> entries,
        bool isAsync)
    {
        if (entries.Count == 0)
            return;

        var callbackMethod = isAsync ? "AsyncHandlers" : "Handlers";
        var registerMethod = isAsync ? "RegisterAsync" : "Register";
        var hasOpenGeneric = false;
        foreach (var entry in entries)
        {
            if (entry.IsOpenGeneric) { hasOpenGeneric = true; break; }
        }

        code.WriteLine($"{paramName}.{callbackMethod}(r =>");
        code.StartBlock();

        // For closed-generic handlers, use the strongly-typed Register<TRequest, TImpl>() method
        // available on the public interface — no implementation cast needed.
        foreach (var entry in entries)
        {
            if (entry.IsOpenGeneric) continue;
            code.WriteLine($"r.{registerMethod}<{entry.RequestTypeFullyQualified}, {entry.HandlerTypeFullyQualified}>();");
        }

        // Open-generic handlers need EnsureHandlerIsRegistered, which only exists on the DI
        // extension's concrete ServiceCollectionSubscriberRegistry. Emit the cast only when at
        // least one open generic is present, so the common case stays interface-only.
        if (hasOpenGeneric)
        {
            code.WriteLine("var registry = (global::Paramore.Brighter.Extensions.DependencyInjection.ServiceCollectionSubscriberRegistry)r;");
            foreach (var entry in entries)
            {
                if (!entry.IsOpenGeneric) continue;
                code.WriteLine($"registry.EnsureHandlerIsRegistered(typeof({entry.HandlerTypeFullyQualified}));");
            }
        }

        code.EndBlock(");");
    }

    private static void WriteMappers(
        CodeWriter code,
        string paramName,
        EquatableArray<MapperEntry> sync,
        EquatableArray<MapperEntry> async)
    {
        if (sync.Count == 0 && async.Count == 0)
            return;

        code.WriteLine($"{paramName}.MapperRegistry(r =>");
        code.StartBlock();

        foreach (var entry in sync)
            code.WriteLine($"r.Add(typeof({entry.RequestTypeFullyQualified}), typeof({entry.MapperTypeFullyQualified}));");

        foreach (var entry in async)
            code.WriteLine($"r.AddAsync(typeof({entry.RequestTypeFullyQualified}), typeof({entry.MapperTypeFullyQualified}));");

        code.EndBlock(");");
    }

    private static void WriteTransforms(
        CodeWriter code,
        string paramName,
        EquatableArray<string> transforms)
    {
        if (transforms.Count == 0)
            return;

        // Transforms is an extension method on IBrighterBuilder; call statically so the
        // generated source doesn't depend on a `using` being added by the consumer.
        code.WriteLine($"global::Paramore.Brighter.Extensions.DependencyInjection.BrighterBuilderExtensions.Transforms({paramName}, r =>");
        code.StartBlock();

        foreach (var transform in transforms)
            code.WriteLine($"r.Add(typeof({transform}));");

        code.EndBlock(");");
    }
}
