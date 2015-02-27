using SendGrid;
using Tasks.Model;

namespace Tasks.Ports
{
    public interface IAmAMailTranslator
    {
        Mail Translate(TaskReminder taskReminder);
    }
}