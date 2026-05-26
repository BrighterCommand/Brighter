using System;

namespace Paramore.Brighter.Transformers.JustSaying;

/// <summary>
/// Decompresses inbound messages produced by JustSaying's publish-side compression
/// (<c>Content-Encoding: gzip,base64</c>). Apply to a mapper's <c>MapToRequest</c>
/// method on the unwrap pipeline. Mirrors
/// <see cref="Paramore.Brighter.Transforms.Attributes.DecompressAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class JustSayingDecompressAttribute : UnwrapWithAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JustSayingDecompressAttribute"/> class.
    /// </summary>
    /// <param name="step">
    /// The step order in the unwrap pipeline. Lower numbers execute earlier; pick a step
    /// that runs before any transform that needs to read the decompressed JSON body.
    /// </param>
    public JustSayingDecompressAttribute(int step) : base(step)
    {
    }

    /// <inheritdoc />
    public override object?[] InitializerParams() => Array.Empty<object?>();

    /// <inheritdoc />
    public override Type GetHandlerType() => typeof(JustSayingCompressionTransform);
}
