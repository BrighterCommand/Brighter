using System.Threading.Tasks;
using TaskMailer.Domain;

namespace TaskMailer.Adapters.MailGateway
{
    public interface IAmAMailGateway
    {
        Task Send(TaskReminder reminder);
    }
}