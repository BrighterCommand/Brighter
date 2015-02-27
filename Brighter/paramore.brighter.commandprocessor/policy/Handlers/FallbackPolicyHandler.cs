using System;
using paramore.brighter.commandprocessor.Logging;
using Polly.CircuitBreaker;

namespace paramore.brighter.commandprocessor.policy.Handlers
{
    public class FallbackPolicyHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        /// <summary>
        /// The Key to the <see cref="IHandleRequests{TRequest}.Context"/> bag item that contains the exception initiating the fallback
        /// </summary>
        public const string CAUSE_OF_FALLBACK_EXCEPTION = "Fallback_Exception_Cause";

        private Func<TRequest, TRequest> _exceptionHandlerFunc;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public FallbackPolicyHandler(ILog logger) : base(logger) {}

        #region Overrides of RequestHandler<TRequest>

        /// <summary>
        /// Initializes from attribute parameters.
        /// </summary>
        /// <param name="initializerList">The initializer list.</param>
        public override void InitializeFromAttributeParams(params object[] initializerList)
        {
            bool catchAll = Convert.ToBoolean(initializerList[0]);
            bool catchBrokenCircuit = Convert.ToBoolean(initializerList[1]);

            if (catchBrokenCircuit == true)
            {
                _exceptionHandlerFunc = CatchBrokenCircuit;
            }
            else if (catchAll == true)
            {
                _exceptionHandlerFunc = CatchAll;
            }
            else
            {
                _exceptionHandlerFunc = CatchNone;
            }
        }


        #endregion

        /// <summary>
        /// Catches any <see cref="Exception"/>s occurring in the pipeline and calls <see cref="IHandleRequests{TRequest}.Fallback"/> to allow handlers in the chain a chance to provide a fallback on failure
        /// The original exception is stored in the <see cref="IHandleRequests{TRequest}.Context"/> under the key <see cref="FallbackPolicyHandler{TRequest}.CAUSE_OF_FALLBACK_EXCEPTION"/> for probing
        /// by handlers in the pipeline called on fallback
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public override TRequest Handle(TRequest command)
        {
            return _exceptionHandlerFunc(command);
        }

        private TRequest CatchAll(TRequest command)
        {
            try
            {
                return base.Handle(command);
            }
            catch (Exception exception)
            {
                Context.Bag.Add(CAUSE_OF_FALLBACK_EXCEPTION, exception);
                return base.Fallback(command);
            }
        }

        private TRequest CatchBrokenCircuit(TRequest command)
        {
            try
            {
                return base.Handle(command);
            }
            catch (BrokenCircuitException brokenCircuitExceptionexception)
            {
                Context.Bag.Add(CAUSE_OF_FALLBACK_EXCEPTION, brokenCircuitExceptionexception);
                return base.Fallback(command);
            }
        }

        private TRequest CatchNone(TRequest command)
        {
            return command;
        }
    }
}
