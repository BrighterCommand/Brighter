using System;

namespace Paramore.Brighter.Inbox.Attributes
{
    /// <summary>
    /// If you configure a global inbox, you may wish to disable it for certain handlers. This attribute lets you do that.
    /// Note that this is not a RequestHandlerAttribute, but a marker attribute whose purpose is to indicate to the pipeline
    /// builder that it should not push an inbox handler into the chain.
    /// There is no async version of this attribute, as we don't need one for a marker interface
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class NoGlobalInboxAttribute : Attribute;
}
