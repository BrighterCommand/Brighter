#region Licence
/* The MIT License (MIT)
Copyright © 2026 Jonny Olliff-Lee <jonny.ollifflee@gmail.com>

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
using System.Reflection;
using Neuroglia.AsyncApi.v3;

namespace Paramore.Brighter.AsyncAPI
{
    /// <summary>
    /// Configuration options for AsyncAPI document generation. Configured via
    /// <c>UseAsyncApi</c> on <c>IBrighterBuilder</c> during service registration.
    /// </summary>
    public class AsyncApiOptions
    {
        /// <summary>Gets or sets the title written to the AsyncAPI document's <c>info.title</c> field.</summary>
        public string Title { get; set; } = "Brighter Application";

        /// <summary>Gets or sets the version written to the AsyncAPI document's <c>info.version</c> field.</summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>Gets or sets the optional description written to the AsyncAPI document's <c>info.description</c> field.</summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the servers to include in the AsyncAPI document, keyed by server id.
        /// </summary>
        public Dictionary<string, V3ServerDefinition>? Servers { get; set; }

        /// <summary>
        /// Gets or sets the assemblies to scan for <c>PublicationTopic</c>-decorated <c>IRequest</c> types
        /// that should appear as publications in the generated document. When null, the default scan set is used.
        /// Ignored if <see cref="DisableAssemblyScanning"/> is <c>true</c>.
        /// </summary>
        public IEnumerable<Assembly>? AssembliesToScan { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether assembly scanning for publication discovery is disabled.
        /// When <c>true</c>, only <see cref="SupplementalPublications"/> and explicitly registered producers contribute publications.
        /// </summary>
        public bool DisableAssemblyScanning { get; set; }

        /// <summary>
        /// Gets or sets additional publications to include alongside those discovered via the producer registry
        /// and assembly scanning. Useful for declaring publications that have no <c>PublicationTopic</c>-decorated type.
        /// </summary>
        public IEnumerable<Publication>? SupplementalPublications { get; set; }
    }
}
