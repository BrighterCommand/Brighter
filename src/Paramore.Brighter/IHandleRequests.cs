#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IHandleRequests
    /// A target of the <see cref="CommandProcessor"/> either as the target of the Command Dispatcher to provide the domain logic required to handle the <see cref="Command"/>
    /// or <see cref="Event"/> or as an orthogonal handler used as part of the Command Processor pipeline.
    /// We recommend deriving your concrete handler from <see cref="RequestHandler{T}"/> instead of implementing the interface as it provides boilerplate
    /// code for calling the next handler in sequence in the pipeline and describing the path
    /// The <see cref="IHandleRequests"/> interface contains a contract not dependant on the <see cref="IRequest"/> and is useful when you need to deal with a handler
    /// without knowing the specific <see cref="IRequest"/> type, but most implementations should use <see cref="IHandleRequests{T}"/> directly
    /// </summary>
    public interface IHandleRequests
    {
        /// <summary>
        /// Gets or sets the context. Usually the context is given to you by the pipeline and you do not need to set this
        /// </summary>
        /// <value>The context.</value>

        IRequestContext Context { get; set; }
        /// <summary>
        /// Describes the path. To support pipeline tracing. Generally return the name of this handler to <see cref="IAmAPipelineTracer"/>,
        ///  or other information to determine the path a request will take
        /// </summary>
        /// <param name="pathExplorer">The path explorer.</param>

        void DescribePath(IAmAPipelineTracer pathExplorer);
        /// <summary>
        /// Initializes from the <see cref="RequestHandlerAttribute"/> attribute parameters. Use when you need to provide parameter information from the
        /// attribute to the handler. Note that the attribute implementation might include types other than primitives that you intend to pass across, but
        /// the attribute itself can only use primitives.
        /// You couple the handler to a specific attribute using this as you need to know about the parameters passed, so this is really only appropriate
        /// for an attribute-handler pair used to provide orthogonal QoS to the pipeline.
        /// </summary>
        /// <param name="initializerList">The initializer list.</param>
        void InitializeFromAttributeParams(params object[] initializerList);

        /// <summary>
        /// Gets the name of the Handler. Useful for diagnostic purposes
        /// </summary>
        /// <value>The name.</value>
        HandlerName Name { get; }

        /// <summary>
        /// Adds to lifetime so that the pipeline can manage destroying handlers created as part of the pipeline by calling the client provided <see cref="IAmAHandlerFactory"/> .
        /// </summary>
        /// <param name="instanceScope">The instance scope.</param>
        void AddToLifetime(IAmALifetime instanceScope);
    }

    /// <summary>
    /// Interface IHandleRequests
    /// A target of the <see cref="CommandProcessor"/> either as the target of the Command Dispatcher to provide the domain logic required to handle the <see cref="Command"/>
    /// or <see cref="Event"/> or as an orthogonal handler used as part of the Command Processor pipeline.
    /// We recommend deriving your concrete handler from <see cref="RequestHandler{T}"/> instead of implementing the interface as it provides boilerplate
    /// code for calling the next handler in sequence in the pipeline and describing the path.
    /// It derives from <see cref="IHandleRequests"/> which provides functionality that is not dependant on <see cref="IRequest"/>. This simplifies some tasks that do not know
    /// the specific type of the <see cref="IRequest"/>
    /// Implementors should use on class to implement both <see cref="IHandleRequests{T}"/> and <see cref="IHandleRequests"/> as per <see cref="RequestHandler{T}"/>
    /// </summary>
    /// <typeparam name="TRequest">The type of the t request.</typeparam>
    public interface IHandleRequests<TRequest> : IHandleRequests where TRequest : class, IRequest
    {
        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>TRequest.</returns>
        TRequest Handle(TRequest request);


        /// <summary>
        /// If a request cannot be completed by <see cref="Handle"/>, implementing the <see cref="Fallback"/> method provides an alternate code path that can be used
        /// This allows for graceful  degradation.  
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>TRequest.</returns>
        TRequest Fallback(TRequest request);

        /// <summary>
        /// Sets the successor.
        /// </summary>
        /// <param name="successor">The successor.</param>
        void SetSuccessor(IHandleRequests<TRequest> successor);
    }
}
