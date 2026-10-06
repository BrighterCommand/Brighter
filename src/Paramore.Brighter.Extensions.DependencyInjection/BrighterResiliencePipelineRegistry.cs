#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

using Paramore.Brighter.Extensions;
using Polly.Registry;

namespace Paramore.Brighter.Extensions.DependencyInjection;

/// <summary>
/// Resolves the registry shared by handlers and outbox producers for one service provider.
/// </summary>
internal sealed class BrighterResiliencePipelineRegistry(
    IBrighterOptions options, ResiliencePipelineRegistry<string>? builderRegistry)
{
    public ResiliencePipelineRegistry<string> Registry { get; } = ResolveRegistry(options, builderRegistry);

    private static ResiliencePipelineRegistry<string> ResolveRegistry(
        IBrighterOptions options, ResiliencePipelineRegistry<string>? builderRegistry)
        => options.ResiliencePipelineRegistry = (builderRegistry
            ?? options.ResiliencePipelineRegistry
            ?? new ResiliencePipelineRegistry<string>()).AddBrighterDefault();
}
