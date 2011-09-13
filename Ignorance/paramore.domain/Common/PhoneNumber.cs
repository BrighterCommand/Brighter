namespace Paramore.Domain.Common
{
    public class PhoneNumber
    {
        private readonly string number = string.Empty;

        public PhoneNumber(string number)
        {
            this.number = number;
        }

        public PhoneNumber() {}

        public static implicit operator string(PhoneNumber phoneNumber)
        {
            return phoneNumber.number;
        }
    }
}