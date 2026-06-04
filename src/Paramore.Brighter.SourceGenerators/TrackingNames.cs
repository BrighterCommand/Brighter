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

namespace Paramore.Brighter.SourceGenerators;

/// <summary>
/// Names attached to the incremental pipeline stages via <c>WithTrackingName</c>. They have no
/// effect on generated output; they exist so incrementality tests can assert that a stage was
/// cached (not re-executed) when an edit doesn't change the semantically relevant shape of the
/// compilation. A stage that flips to <c>Modified</c> on an unrelated edit is the classic symptom
/// of a pipeline value that lost its value equality.
/// </summary>
public static class TrackingNames
{
    /// <summary>The per-method <c>[BrighterRegistrations]</c> projection (<c>MethodCandidate</c>).</summary>
    public const string MethodCandidates = "MethodCandidates";

    /// <summary>The per-class discovery projection (<c>DiscoveryBatch</c>).</summary>
    public const string DiscoveryBatches = "DiscoveryBatches";

    /// <summary>The flattened, sorted registration entries for the whole compilation.</summary>
    public const string DiscoveredEntries = "DiscoveredEntries";

    /// <summary>The per-method candidate combined with the discovered entries, ready to emit.</summary>
    public const string RegistrationInputs = "RegistrationInputs";
}
