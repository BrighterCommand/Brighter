using System.IO;
using System.Reflection;
using Simple.Data;
using paramore.commandprocessor;
using tasklist.web.Commands;

namespace tasklist.web.Handlers
{
    public class BeginTransaction<TRequest> : RequestHandler<TRequest>
        where TRequest: class, IRequest, ICanBeValidated
    {
        static readonly string DatabasePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Substring(8)),"tasks.sqlite");

        public override TRequest Handle(TRequest command)
        {
            Database db = Database.Opener.OpenFile(DatabasePath);
            Context.Bag.Db = db;
            Context.Bag.Tx = db.BeginTransaction(); 
            return base.Handle(command);
        }
    }
}