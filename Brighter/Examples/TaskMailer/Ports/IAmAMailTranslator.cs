using SendGrid;
using TaskMailer.Domain;

namespace TaskMailer.Ports
{
    public interface IAmAMailTranslator
    {
        Mail Translate(TaskReminder taskReminder);
    }
}