using System.Text;

namespace paramore.commandprocessor
{
    internal class ChainPathExplorer : IChainPathExplorer
    {
        private readonly StringBuilder buffer = new StringBuilder();

        public void AddToPath(HandlerName handlerName)
        {
            buffer.AppendFormat("{0}|", handlerName);
        }

        public override string ToString()
        {
            return buffer.ToString();
        }
    }
}