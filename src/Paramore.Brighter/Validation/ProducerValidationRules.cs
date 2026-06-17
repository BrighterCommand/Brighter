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

using System.Linq;

namespace Paramore.Brighter.Validation;

/// <summary>
/// Provides validation specifications for producer publications. Each method returns
/// an <see cref="ISpecification{T}"/> that evaluates a <see cref="Publication"/>
/// and reports validation findings via the visitor pattern.
/// </summary>
public static class ProducerValidationRules
{
    /// <summary>
    /// Validates that <see cref="Publication.RequestType"/> is not null.
    /// A null RequestType means Post()/Deposit() will throw a ConfigurationException at runtime.
    /// </summary>
    /// <returns>A simple specification that reports an Error when RequestType is null.</returns>
    public static ISpecification<Publication> PublicationRequestTypeSet()
        => new Specification<Publication>(
            p => p.RequestType != null,
            p => new ValidationError(
                ValidationSeverity.Error,
                $"Publication '{p.Topic}'",
                "Publication.RequestType is null — Post()/Deposit() will throw ConfigurationException"));

    /// <summary>
    /// Validates that <see cref="Publication.RequestType"/> implements <see cref="IRequest"/>.
    /// A RequestType that is not null but does not implement IRequest will fail at runtime.
    /// Vacuously passes when RequestType is null (caught by <see cref="PublicationRequestTypeSet"/>).
    /// </summary>
    /// <returns>A simple specification that reports an Error when RequestType does not implement IRequest.</returns>
    public static ISpecification<Publication> PublicationRequestTypeImplementsIRequest()
        => new Specification<Publication>(
            p => p.RequestType == null || typeof(IRequest).IsAssignableFrom(p.RequestType),
            p => new ValidationError(
                ValidationSeverity.Error,
                $"Publication '{p.Topic}'",
                $"Publication.RequestType '{p.RequestType!.FullName}' does not implement IRequest"));

    /// <summary>
    /// Validates that every wrap transform the publication's resolved mapper declares can be resolved.
    /// A declared transform whose transformer type is not registered is a strong signal that its assembly
    /// was not scanned. Reports one <see cref="ValidationSeverity.Warning"/> per
    /// unresolvable transform, naming the request type, the transformer type, the topic, and prompting an
    /// <c>AutoFromAssemblies</c> check. Publications with a null <see cref="Publication.RequestType"/>, whose
    /// request type resolves to no mapper, or whose request type resolves to the default mapper (whose
    /// transforms are Brighter built-ins and out of scope) are skipped.
    /// </summary>
    /// <param name="mapperRegistry">The mapper registry used to describe the publication's transforms.</param>
    /// <param name="probe">Answers whether a declared transformer type is resolvable, without instantiating it.</param>
    /// <returns>A specification that reports a Warning per unresolvable wrap transform.</returns>
    public static ISpecification<Publication> WrapTransformResolvable(
        MessageMapperRegistry mapperRegistry, IAmATransformerResolvabilityProbe probe)
        => new Specification<Publication>(publication =>
        {
            if (publication.RequestType is null)
                return [];

            var description = TransformPipelineBuilder.DescribeTransforms(
                mapperRegistry, publication.RequestType, includeAsync: true);

            if (description is null || description.IsDefaultMapper)
                return [];

            return description.WrapTransforms
                .Where(step => !probe.Resolves(step.TransformType))
                .Select(step => ValidationResult.Fail(new ValidationError(
                    ValidationSeverity.Warning,
                    $"Publication '{publication.Topic}'",
                    $"Request '{publication.RequestType.Name}' declares wrap transform '{step.TransformType.Name}' " +
                    $"on topic '{publication.Topic}' — that transformer is not registered. " +
                    "Is its assembly included in AutoFromAssemblies()?")))
                .ToList();
        });
}
