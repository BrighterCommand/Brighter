using System.Text;

namespace paramore.commandprocessor
{
    public class ChainPathExplorer
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