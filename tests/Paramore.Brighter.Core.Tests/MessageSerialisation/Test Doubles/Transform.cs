namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

/// <summary>
/// Base class for transforms
/// For simple transforms that neither have initializer params or manage resources, this base class prevents the need to implement no-op interface methods
/// </summary>
public abstract class Transform : IAmAMessageTransform
{
    /// <summary>
    /// Gets or sets the context. Usually the context is given to you by the pipeline and you do not need to set this
    /// </summary>
    /// <value>The context.</value>
    public IRequestContext Context { get; set; }
    
    /// <summary>
    /// Dispose cleans up unmanaged resources
    /// This base class version is a no-op
    /// </summary>
    public void Dispose() { }

    /// <summary>
    /// Initialize from Attributes allows attribute parameters to be passed to the type associated with the attribute
    /// This base class version is a no-op
    /// </summary>
    /// <param name="initializerList"></param>
    public void InitializeWrapFromAttributeParams(params object[] initializerList) { }

    /// <summary>
    /// Initialize from Attributes allows attribute parameters to be passed to the type associated with the attribute
    /// This base class version is a no-op
    /// </summary>
    /// <param name="initializerList"></param>
    public void InitializeUnwrapFromAttributeParams(params object[] initializerList) { }

    public abstract Message Wrap(Message message, Publication publication);

    public abstract Message Unwrap(Message message);
}
