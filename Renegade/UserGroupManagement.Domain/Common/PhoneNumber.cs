namespace UserGroupManagement.Domain.Common
{
    public class PhoneNumber
    {
        public string Number { get; private set; }

        public PhoneNumber(string number)
        {
            this.Number = number;
        }
    }
}