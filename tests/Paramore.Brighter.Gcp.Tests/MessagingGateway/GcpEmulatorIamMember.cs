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

using System;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;

/// <summary>
/// The IAM member the GCP conformance providers set on a DLQ-backed subscription.
/// </summary>
/// <remarks>
/// On the emulator, a non-empty member stops the gateway calling Cloud Resource Manager to derive the
/// project's Pub/Sub service agent (R-27(b), C-11). On real Pub/Sub, IAM grants are validated, so a
/// made-up service account fails channel creation. There the member is left unset, and the gateway
/// derives the real service agent itself.
/// </remarks>
internal static class GcpEmulatorIamMember
{
    private const string EmulatorMember = "serviceAccount:brighter-pubsub@brighter-test.iam.gserviceaccount.com";

    /// <summary>
    /// The member to configure: a placeholder service account on the emulator, <see langword="null"/>
    /// on real Pub/Sub.
    /// </summary>
    public static string? Value =>
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PUBSUB_EMULATOR_HOST"))
            ? null
            : EmulatorMember;
}
