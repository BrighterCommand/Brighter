using Tasks.Model;
using Task = System.Threading.Tasks.Task;

namespace Tasks.Adapters.MailGateway
{
    public interface IAmAMailGateway
    {
        Task Send(TaskReminder reminder);
    }
}