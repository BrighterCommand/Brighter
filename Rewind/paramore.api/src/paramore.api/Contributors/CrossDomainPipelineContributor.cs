using OpenRasta.Pipeline;
using OpenRasta.Web;

//see: https://groups.google.com/forum/#!searchin/openeverything-dev/origin/openeverything-dev/NARQHigzNQk/2DRNt5ncjkUJ

namespace Paramore.Adapters.Presentation.API.Contributors
{
  public class CrossDomainPipelineContributor : IPipelineContributor 
    {
        public void Initialize(IPipeline pipelineRunner)
        {
            pipelineRunner.Notify(processOptions).Before<KnownStages.IUriMatching>();
        }
        private PipelineContinuation processOptions(ICommunicationContext context)
        {
            addHeaders(context);
            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = 200;
                context.OperationResult = new OperationResult.NoContent();
                return PipelineContinuation.RenderNow;
            }
            return PipelineContinuation.Continue;
        }
        private void addHeaders(ICommunicationContext context)
        {
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods","POST, GET, OPTIONS, PUT, DELETE");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
        }
    }
}